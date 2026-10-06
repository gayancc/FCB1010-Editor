using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace FCB1010.Midi;

public enum TransportState { Disconnected, Connected, Receiving, Sending, Error }
public sealed record MidiPortInfo(string Id, string Name);
public sealed record MidiDiagnostic(DateTimeOffset Timestamp, string Direction, string MessageType, int Size, string Status, string? Hex = null);
public sealed record SysExTransferResult(bool Completed, byte[]? Data, string Message);

public interface IMidiTransport : IDisposable
{
    TransportState State { get; }
    string? InputPortName { get; }
    string? OutputPortName { get; }
    event EventHandler<TransportState>? StateChanged;
    event EventHandler<MidiDiagnostic>? Diagnostic;
    event EventHandler<byte[]>? SysExReceived;
    IReadOnlyList<MidiPortInfo> GetInputPorts();
    IReadOnlyList<MidiPortInfo> GetOutputPorts();
    Task ConnectAsync(string inputId, string outputId, CancellationToken ct = default);
    Task DisconnectAsync();
    Task SendSysExAsync(byte[] message, CancellationToken ct = default);
    Task SendMidiEventAsync(MidiEvent midiEvent, CancellationToken ct = default);
}

public sealed class SysExAssembler
{
    private readonly List<byte> _buffer = [];
    private DateTimeOffset? _started;
    public bool IsReceiving => _started is not null;
    public int ReceivedBytes => _buffer.Count;

    public byte[]? Feed(ReadOnlySpan<byte> fragment, DateTimeOffset now)
    {
        byte[]? complete = null;
        foreach (var value in fragment)
        {
            if (value == 0xF0) { _buffer.Clear(); _started = now; _buffer.Add(value); continue; }
            if (_started is null) continue;
            if (value > 0x7F && value != 0xF7) { Reset(); throw new InvalidDataException($"Unexpected status byte 0x{value:X2} inside SysEx."); }
            _buffer.Add(value);
            if (value == 0xF7) { complete = _buffer.ToArray(); Reset(); }
        }
        return complete;
    }

    public string? CheckTimeout(DateTimeOffset now, TimeSpan timeout)
    {
        if (_started is null || now - _started <= timeout) return null;
        var message = $"SysEx reception started but only {_buffer.Count:N0} bytes were received before the {timeout.TotalSeconds:0.#}-second timeout.";
        Reset(); return message;
    }
    public void Reset() { _buffer.Clear(); _started = null; }
}

public sealed class DryWetMidiTransport : IMidiTransport
{
    private InputDevice? _input;
    private OutputDevice? _output;
    private readonly SysExAssembler _assembler = new();
    private readonly object _sendGate = new();
    public TransportState State { get; private set; }
    public string? InputPortName => _input?.Name;
    public string? OutputPortName => _output?.Name;
    public event EventHandler<TransportState>? StateChanged;
    public event EventHandler<MidiDiagnostic>? Diagnostic;
    public event EventHandler<byte[]>? SysExReceived;

    public IReadOnlyList<MidiPortInfo> GetInputPorts() => InputDevice.GetAll().Select((d, i) => new MidiPortInfo(i.ToString(), d.Name)).ToArray();
    public IReadOnlyList<MidiPortInfo> GetOutputPorts() => OutputDevice.GetAll().Select((d, i) => new MidiPortInfo(i.ToString(), d.Name)).ToArray();

    public Task ConnectAsync(string inputId, string outputId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            DisconnectCore();
            try
            {
                _input = InputDevice.GetByIndex(int.Parse(inputId));
                _output = OutputDevice.GetByIndex(int.Parse(outputId));
                _input.EventReceived += OnEventReceived; _input.ErrorOccurred += OnInputError; _output.ErrorOccurred += OnOutputError;
                _input.StartEventsListening(); _output.PrepareForEventsSending();
                SetState(TransportState.Connected);
                Log("SYSTEM", "Connection", 0, $"Connected: IN '{_input.Name}', OUT '{_output.Name}'.");
            }
            catch (Exception ex) { DisconnectCore(); SetState(TransportState.Error); Log("SYSTEM", "Connection", 0, ex.Message); throw; }
        }, ct);
    }

    public Task DisconnectAsync() { DisconnectCore(); SetState(TransportState.Disconnected); return Task.CompletedTask; }

    public Task SendSysExAsync(byte[] message, CancellationToken ct = default)
    {
        if (_output is null) throw new InvalidOperationException("Select MIDI IN and MIDI OUT and connect first.");
        if (message.Length < 2 || message[0] != 0xF0 || message[^1] != 0xF7) throw new ArgumentException("A complete F0...F7 SysEx message is required.", nameof(message));
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested(); SetState(TransportState.Sending);
            try
            {
                lock (_sendGate) _output.SendEvent(new NormalSysExEvent(message[1..]));
                Log("OUT", "SysEx", message.Length, $"TX SysEx via '{_output.Name}'.", Convert.ToHexString(message.AsSpan(0, Math.Min(message.Length, 64))) + (message.Length > 64 ? "…" : ""));
                SetState(TransportState.Connected);
            }
            catch (Exception ex) { SetState(TransportState.Error); Log("OUT", "SysEx", message.Length, ex.Message); throw; }
        }, ct);
    }

    public Task SendMidiEventAsync(MidiEvent midiEvent, CancellationToken ct = default)
    {
        if (_output is null) throw new InvalidOperationException("Select MIDI IN and MIDI OUT and connect first.");
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_sendGate) _output.SendEvent(midiEvent);
            Log("OUT", midiEvent.EventType.ToString(), 0, "Live preview channel-voice event.", midiEvent.ToString());
        }, ct);
    }

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        try
        {
            if (e.Event is not SysExEvent sysEx) return;
            SetState(TransportState.Receiving);
            var fragment = e.Event is NormalSysExEvent ? new byte[] { 0xF0 }.Concat(sysEx.Data).ToArray() : sysEx.Data;
            var completed = _assembler.Feed(fragment, DateTimeOffset.UtcNow);
            Log("IN", e.Event.GetType().Name, fragment.Length, completed is null ? "Fragment received." : "Complete SysEx received.", Convert.ToHexString(fragment));
            if (completed is not null) { SetState(TransportState.Connected); SysExReceived?.Invoke(this, completed); }
        }
        catch (Exception ex) { SetState(TransportState.Error); Log("IN", "SysEx", _assembler.ReceivedBytes, ex.Message); }
    }

    private void OnInputError(object? sender, ErrorOccurredEventArgs e) { SetState(TransportState.Error); Log("IN", "Driver", 0, e.Exception.Message); }
    private void OnOutputError(object? sender, ErrorOccurredEventArgs e) { SetState(TransportState.Error); Log("OUT", "Driver", 0, e.Exception.Message); }
    private void Log(string direction, string type, int size, string status, string? hex = null) => Diagnostic?.Invoke(this, new(DateTimeOffset.Now, direction, type, size, status, hex));
    private void SetState(TransportState state) { State = state; StateChanged?.Invoke(this, state); }
    private void DisconnectCore() { if (_input is not null) { _input.EventReceived -= OnEventReceived; _input.ErrorOccurred -= OnInputError; _input.Dispose(); } if (_output is not null) { _output.ErrorOccurred -= OnOutputError; _output.Dispose(); } _input = null; _output = null; _assembler.Reset(); }
    public void Dispose() => DisconnectCore();
}
