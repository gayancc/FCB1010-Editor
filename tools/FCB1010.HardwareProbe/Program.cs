using System.Text.Json;
using FCB1010.Core;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("MIDI INPUTS");
    foreach (var (device, index) in InputDevice.GetAll().Select((d, i) => (d, i))) Console.WriteLine($"  [{index}] {device.Name}");
    Console.WriteLine("MIDI OUTPUTS");
    foreach (var (device, index) in OutputDevice.GetAll().Select((d, i) => (d, i))) Console.WriteLine($"  [{index}] {device.Name}");
    return;
}

if (args[0].Equals("diff", StringComparison.OrdinalIgnoreCase) && args.Length >= 3)
{
    var a = await File.ReadAllBytesAsync(args[1]);
    var b = await File.ReadAllBytesAsync(args[2]);
    FcbSysExCodec.ValidateEnvelope(a); FcbSysExCodec.ValidateEnvelope(b);
    var da = FcbSysExCodec.Decode(a); var db = FcbSysExCodec.Decode(b);
    Console.WriteLine("--- transport ---");
    foreach (var d in DumpComparer.Compare(a, b).Take(80))
        Console.WriteLine($"  T@{d.Offset:X4} {d.OldByte:X2}->{d.NewByte:X2} xor={d.Xor:X2} {d.LikelyField}");
    Console.WriteLine("--- decoded ---");
    Console.Write(DecodedMemoryLab.FormatDiffReport(DecodedMemoryLab.Diff(da, db)));
    return;
}

if (args[0].Equals("decode", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
{
    var raw = await File.ReadAllBytesAsync(args[1]);
    var decoded = FcbSysExCodec.Decode(raw);
    var start = args.Length >= 3 ? int.Parse(args[2], System.Globalization.NumberStyles.HexNumber) : 0;
    var count = args.Length >= 4 ? int.Parse(args[3]) : 64;
    foreach (var row in DecodedMemoryLab.Inspect(decoded, start, count))
        Console.WriteLine($"{row.Offset:X4}  {row.Hex}  {row.Binary}  {row.Dec,3}  [{row.Status}]  {row.Meaning}");
    return;
}

if (args[0].Equals("request", StringComparison.OrdinalIgnoreCase) && args.Length >= 5)
{
    var requestInput = int.Parse(args[1]); var requestOutput = int.Parse(args[2]);
    var requestBytes = Convert.FromHexString(args[3].Replace(" ", "")); var requestSeconds = int.Parse(args[4]);
    if (requestBytes[0] != 0xF0 || requestBytes[^1] != 0xF7) throw new ArgumentException("Request must be a complete F0...F7 SysEx message.");
    using var requestInDevice = InputDevice.GetByIndex(requestInput); using var requestOutDevice = OutputDevice.GetByIndex(requestOutput);
    requestInDevice.EventReceived += (_, e) =>
    {
        Console.WriteLine($"{DateTimeOffset.UtcNow:O} {e.Event.GetType().Name}: {e.Event}");
        if (e.Event is SysExEvent sx) Console.WriteLine("  DATA=" + Convert.ToHexString(sx.Data));
    };
    requestInDevice.ErrorOccurred += (_, e) => Console.Error.WriteLine("MIDI IN ERROR: " + e.Exception.Message);
    requestOutDevice.ErrorOccurred += (_, e) => Console.Error.WriteLine("MIDI OUT ERROR: " + e.Exception.Message);
    requestInDevice.StartEventsListening(); requestOutDevice.PrepareForEventsSending();
    requestOutDevice.SendEvent(new NormalSysExEvent(requestBytes[1..]));
    Console.WriteLine($"SENT {Convert.ToHexString(requestBytes)} to [{requestOutput}] {requestOutDevice.Name}; listening {requestSeconds}s on [{requestInput}] {requestInDevice.Name}...");
    await Task.Delay(TimeSpan.FromSeconds(requestSeconds));
    requestInDevice.StopEventsListening(); return;
}

if (args[0].Equals("send-hex", StringComparison.OrdinalIgnoreCase) && args.Length >= 3)
{
    var sendOutput = int.Parse(args[1]);
    var bytes = Path.GetExtension(args[2]).Equals(".syx", StringComparison.OrdinalIgnoreCase)
        ? await File.ReadAllBytesAsync(args[2])
        : Convert.FromHexString((await File.ReadAllTextAsync(args[2])).Trim());
    FcbSysExCodec.ValidateEnvelope(bytes);
    using var sendDevice = OutputDevice.GetByIndex(sendOutput); sendDevice.PrepareForEventsSending();
    sendDevice.SendEvent(new NormalSysExEvent(bytes[1..]));
    Console.WriteLine($"SENT validated {bytes.Length}-byte FCB1010 dump to [{sendOutput}] {sendDevice.Name}."); return;
}

if (args[0].Equals("scan", StringComparison.OrdinalIgnoreCase) && args.Length >= 3)
{
    var scanInput = int.Parse(args[1]); var scanOutput = int.Parse(args[2]);
    using var scanInDevice = InputDevice.GetByIndex(scanInput); using var scanOutDevice = OutputDevice.GetByIndex(scanOutput);
    var currentCommand = -1;
    var foundDump = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    scanInDevice.EventReceived += (_, e) =>
    {
        if (e.Event is not SysExEvent sx) return;
        var raw = new byte[sx.Data.Length + 1]; raw[0] = 0xF0; sx.Data.CopyTo(raw, 1);
        Console.WriteLine($"CMD={currentCommand:X2} LEN={raw.Length} DATA={Convert.ToHexString(raw.AsSpan(0, Math.Min(raw.Length, 40)))}{(raw.Length > 40 ? "..." : "")}");
        if (raw.Length == FcbSysExCodec.DumpLength) foundDump.TrySetResult(raw);
    };
    scanInDevice.ErrorOccurred += (_, e) => Console.Error.WriteLine("MIDI IN ERROR: " + e.Exception.Message);
    scanOutDevice.ErrorOccurred += (_, e) => Console.Error.WriteLine("MIDI OUT ERROR: " + e.Exception.Message);
    scanInDevice.StartEventsListening(); scanOutDevice.PrepareForEventsSending();
    Console.WriteLine($"Scanning FCB1010 one-byte commands 00-7F via [{scanOutput}] {scanOutDevice.Name}...");
    for (currentCommand = 0; currentCommand <= 0x7F && !foundDump.Task.IsCompleted; currentCommand++)
    {
        scanOutDevice.SendEvent(new NormalSysExEvent([0x00, 0x20, 0x32, 0x01, 0x0C, (byte)currentCommand, 0xF7]));
        await Task.Delay(300);
    }
    await Task.Delay(1000);
    scanInDevice.StopEventsListening();
    if (foundDump.Task.IsCompletedSuccessfully)
    {
        var path = Path.GetFullPath($"FCB1010-scan-{DateTime.Now:yyyyMMdd-HHmmss}.syx");
        await File.WriteAllBytesAsync(path, foundDump.Task.Result);
        Console.WriteLine($"FULL DUMP SAVED: {path}");
    }
    return;
}

if (args[0].Equals("request-dump", StringComparison.OrdinalIgnoreCase) && args.Length >= 4)
{
    var dumpInput = int.Parse(args[1]); var dumpOutput = int.Parse(args[2]); var dumpPath = Path.GetFullPath(args[3]);
    using var dumpInDevice = InputDevice.GetByIndex(dumpInput); using var dumpOutDevice = OutputDevice.GetByIndex(dumpOutput);
    var dumpReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    dumpInDevice.EventReceived += (_, e) =>
    {
        if (e.Event is not SysExEvent sx) return;
        var raw = new byte[sx.Data.Length + 1]; raw[0] = 0xF0; sx.Data.CopyTo(raw, 1);
        if (raw.Length == FcbSysExCodec.DumpLength) dumpReceived.TrySetResult(raw);
    };
    dumpInDevice.StartEventsListening(); dumpOutDevice.PrepareForEventsSending();
    dumpOutDevice.SendEvent(new NormalSysExEvent([0x00, 0x20, 0x32, 0x01, 0x0C, 0x4F, 0xF7]));
    try
    {
        var raw = await dumpReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        FcbSysExCodec.ValidateEnvelope(raw); await File.WriteAllBytesAsync(dumpPath, raw);
        Console.WriteLine($"SAVED {raw.Length}-byte validated dump: {dumpPath}");
    }
    catch (TimeoutException) { Console.Error.WriteLine("Timed out waiting for a complete FCB1010 dump."); Environment.ExitCode = 1; }
    finally { dumpInDevice.StopEventsListening(); }
    return;
}

if (args[0].Equals("roundtrip-test", StringComparison.OrdinalIgnoreCase) && args.Length >= 4)
{
    var testInput = int.Parse(args[1]); var testOutput = int.Parse(args[2]); var originalPath = Path.GetFullPath(args[3]);
    var original = await File.ReadAllBytesAsync(originalPath); FcbSysExCodec.ValidateEnvelope(original);
    var configuration = FcbSysExCodec.Parse(original, FirmwareFamily.UnO);
    var oldProgram = configuration.GetPreset(0, 1).ProgramChanges[0].Program;
    var testProgram = oldProgram == 99 ? 98 : 99;
    configuration.GetPreset(0, 1).ProgramChanges[0].Program = testProgram;
    var modified = FcbSysExCodec.Serialize(configuration);
    using var testInDevice = InputDevice.GetByIndex(testInput); using var testOutDevice = OutputDevice.GetByIndex(testOutput);
    var pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    testInDevice.EventReceived += (_, e) =>
    {
        if (e.Event is not SysExEvent sx) return;
        var raw = new byte[sx.Data.Length + 1]; raw[0] = 0xF0; sx.Data.CopyTo(raw, 1);
        if (raw.Length == FcbSysExCodec.DumpLength) pending.TrySetResult(raw);
    };
    testInDevice.StartEventsListening(); testOutDevice.PrepareForEventsSending();
    async Task<byte[]> ReadBack()
    {
        pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        testOutDevice.SendEvent(new NormalSysExEvent([0x00, 0x20, 0x32, 0x01, 0x0C, 0x4F, 0xF7]));
        return await pending.Task.WaitAsync(TimeSpan.FromSeconds(12));
    }
    try
    {
        Console.WriteLine($"Writing controlled test: preset 00-1 PC1 {oldProgram} -> {testProgram}...");
        testOutDevice.SendEvent(new NormalSysExEvent(modified[1..])); await Task.Delay(750);
        var modifiedReadback = await ReadBack();
        if (!modified.SequenceEqual(modifiedReadback)) throw new InvalidOperationException($"Modified read-back differs at {modified.Zip(modifiedReadback).TakeWhile(x => x.First == x.Second).Count()}.");
        Console.WriteLine("MODIFIED READ-BACK: byte-for-byte identical.");
        Console.WriteLine("Restoring original dump...");
        testOutDevice.SendEvent(new NormalSysExEvent(original[1..])); await Task.Delay(750);
        var restoredReadback = await ReadBack();
        if (!original.SequenceEqual(restoredReadback)) throw new InvalidOperationException($"Restore read-back differs at {original.Zip(restoredReadback).TakeWhile(x => x.First == x.Second).Count()}.");
        Console.WriteLine("RESTORE READ-BACK: byte-for-byte identical. Hardware round trip passed.");
    }
    finally { testInDevice.StopEventsListening(); }
    return;
}

if (!args[0].Equals("capture", StringComparison.OrdinalIgnoreCase) || args.Length < 4)
{
    Console.Error.WriteLine("Usage: list | diff <a.syx> <b.syx> | decode <syx> [hexStart] [count] | capture <input-index> <seconds> <output-directory> | request <input-index> <output-index> <hex> <seconds> | request-dump <input-index> <output-index> <syx-file> | send-hex <output-index> <hex-or-syx-file> | scan <input-index> <output-index> | roundtrip-test <input-index> <output-index> <baseline-syx>");
    Environment.ExitCode = 2; return;
}

var inputIndex = int.Parse(args[1]);
var duration = TimeSpan.FromSeconds(int.Parse(args[2]));
var outputDirectory = Path.GetFullPath(args[3]);
Directory.CreateDirectory(outputDirectory);
using var input = InputDevice.GetByIndex(inputIndex);
var messages = new List<object>();
var completed = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
var started = DateTimeOffset.UtcNow;

input.EventReceived += (_, e) =>
{
    var timestamp = DateTimeOffset.UtcNow;
    var entry = new { timestamp, elapsedMs = (timestamp - started).TotalMilliseconds, type = e.Event.GetType().Name, text = e.Event.ToString() };
    lock (messages) messages.Add(entry);
    Console.WriteLine($"{timestamp:O} {e.Event.GetType().Name}: {e.Event}");
    if (e.Event is NormalSysExEvent sysEx)
    {
        var rawList = new List<byte> { 0xF0 }; rawList.AddRange(sysEx.Data);
        if (rawList[^1] != 0xF7) rawList.Add(0xF7);
        var raw = rawList.ToArray();
        Console.WriteLine($"  SysEx bytes={raw.Length}, head={Convert.ToHexString(raw.AsSpan(0, Math.Min(16, raw.Length)))}");
        if (raw.Length == FcbSysExCodec.DumpLength) completed.TrySetResult(raw);
    }
};
input.ErrorOccurred += (_, e) => Console.Error.WriteLine("MIDI ERROR: " + e.Exception.Message);
input.StartEventsListening();
Console.WriteLine($"Listening on [{inputIndex}] {input.Name} for {duration.TotalSeconds:0} seconds...");
using var timeout = new CancellationTokenSource(duration);
try
{
    var raw = await completed.Task.WaitAsync(timeout.Token);
    var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    var syxPath = Path.Combine(outputDirectory, $"FCB1010-hardware-{stamp}.syx");
    await File.WriteAllBytesAsync(syxPath, raw);
    var config = FcbSysExCodec.Parse(raw, FirmwareFamily.UnO);
    var projectPath = Path.Combine(outputDirectory, $"FCB1010-hardware-{stamp}.fcbproject");
    await ProjectPersistence.SaveProjectAsync(projectPath, config);
    Console.WriteLine($"VALID FCB1010 DUMP SAVED: {syxPath}");
    Console.WriteLine($"PROJECT SAVED: {projectPath}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Capture window ended without a complete 2,352-byte FCB1010 dump.");
}
finally
{
    input.StopEventsListening();
    var logPath = Path.Combine(outputDirectory, $"midi-capture-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    await File.WriteAllTextAsync(logPath, JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"EVENT LOG: {logPath}");
}
