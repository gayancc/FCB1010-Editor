using FCB1010.Core;

namespace FCB1010.Midi;

public enum DumpTransferMode { Full, Chunked }

public sealed class FcbDeviceService(IMidiTransport transport, string backupDirectory) : IDisposable
{
    public static readonly byte[] FirmwareVersionRequest = [0xF0, 0x00, 0x20, 0x32, 0x01, 0x0C, 0x40, 0xF7];
    public static readonly byte[] CurrentPatchRequest = [0xF0, 0x00, 0x20, 0x32, 0x01, 0x0C, 0x45, 0xF7];
    public static readonly byte[] FullDumpRequest = [0xF0, 0x00, 0x20, 0x32, 0x01, 0x0C, 0x4F, 0xF7];
    public const int ChunkLength = 160;
    public const int ChunkCount = 16;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[]? _lastReceived;
    public byte[]? LastReceived => _lastReceived?.ToArray();

    /// <summary>Production default is Full. Chunked framing is hardware-observed (cmd 50→10) but assembly on WIDI is not yet verified byte-exact.</summary>
    public DumpTransferMode PreferredReadMode { get; set; } = DumpTransferMode.Full;
    public DumpTransferMode PreferredWriteMode { get; set; } = DumpTransferMode.Full;
    public TimeSpan PostWriteSettle { get; set; } = TimeSpan.FromMilliseconds(500);

    public async Task<SysExTransferResult> ReceiveDumpAsync(TimeSpan timeout, FirmwareFamily family, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (PreferredReadMode == DumpTransferMode.Chunked)
                return new(false, null,
                    "Chunked read is not enabled as a production path yet. Commands 50–5F return 160-byte replies (verified), but reconstructing a byte-exact 2,352-byte dump from those replies is still UNVERIFIED on this interface. Use Full mode (4F).");
            return await ReceiveFullDumpAsync(timeout, family, requestUpload: true, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<FirmwareProbeResult> ProbeFirmwareAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object? _, byte[] data)
            {
                if (data.Length == 9 && data.AsSpan(0, 6).SequenceEqual(FirmwareVersionRequest.AsSpan(0, 6)))
                    tcs.TrySetResult(data);
            }
            transport.SysExReceived += Handler;
            try
            {
                await transport.SendSysExAsync(FirmwareVersionRequest, ct);
                var response = await tcs.Task.WaitAsync(timeout, ct);
                var signature = $"{response[6]:X2} {response[7]:X2}";
                var family = response[7] == 0x0E ? FirmwareFamily.UnO : FirmwareFamily.Stock;
                return new(true, family, signature,
                    $"FCB1010 replied to the firmware probe ({signature}); heuristic family={family}. Use manual firmware selection if this is wrong.");
            }
            catch (TimeoutException)
            {
                return new(false, FirmwareFamily.Unknown, null,
                    "No FCB1010 firmware reply was received within the timeout. Check both MIDI directions and port selection.");
            }
            finally { transport.SysExReceived -= Handler; }
        }
        finally { _gate.Release(); }
    }

    public async Task<SysExTransferResult> WriteDumpAsync(FcbConfiguration config, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (PreferredWriteMode == DumpTransferMode.Chunked)
                return new(false, null,
                    "Chunked write is intentionally disabled until a ControlCenter-compatible assembly/split is hardware-proven. Use Full 2,352-byte write.");

            var bytes = FcbSysExCodec.Serialize(config);
            if (_lastReceived is not null)
            {
                Directory.CreateDirectory(backupDirectory);
                var path = Path.Combine(backupDirectory, $"FCB1010-{DateTime.Now:yyyyMMdd-HHmmss-fff}.syx");
                await File.WriteAllBytesAsync(path, _lastReceived, ct);
            }

            await transport.SendSysExAsync(bytes, ct);
            await Task.Delay(PostWriteSettle, ct);
            var readback = await ReceiveFullDumpAsync(TimeSpan.FromSeconds(12), config.Firmware, requestUpload: true, ct);
            if (!readback.Completed || readback.Data is null)
                return new(false, bytes, "The dump was transmitted, but automatic read-back verification timed out. The device state is unverified.");

            var differences = DumpComparer.Compare(bytes, readback.Data);
            return differences.Count == 0
                ? new(true, bytes, "Write succeeded; FCB1010 read-back is byte-for-byte identical.")
                : new(false, readback.Data, $"The device replied after the write, but verification found {differences.Count} differing byte(s). First @{differences[0].Offset} ({differences[0].LikelyField ?? "unmapped"}).");
        }
        catch (Exception ex) { return new(false, null, $"Write did not complete: {ex.Message}"); }
        finally { _gate.Release(); }
    }

    /// <summary>Requests chunks 50–5F and returns raw replies for protocol lab analysis. Does not claim a reconstructed dump.</summary>
    public async Task<ChunkCaptureResult> CaptureChunksForLabAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var chunks = new byte[ChunkCount][];
            var remaining = ChunkCount;
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object? _, byte[] data)
            {
                if (data.Length != ChunkLength) return;
                if (data[6] is < 0x10 or > 0x1F) return;
                var idx = data[6] - 0x10;
                if (chunks[idx] is not null) return;
                chunks[idx] = data;
                if (Interlocked.Decrement(ref remaining) == 0) tcs.TrySetResult();
            }
            transport.SysExReceived += Handler;
            try
            {
                for (var i = 0; i < ChunkCount; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await transport.SendSysExAsync([0xF0, 0x00, 0x20, 0x32, 0x01, 0x0C, (byte)(0x50 + i), 0xF7], ct);
                    await Task.Delay(80, ct);
                }
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);
                await tcs.Task.WaitAsync(timeoutCts.Token);
                return new(true, chunks!, $"Captured {ChunkCount} raw {ChunkLength}-byte chunk replies for lab analysis.");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                var got = chunks.Count(c => c is not null);
                return new(false, chunks.Where(c => c is not null).Cast<byte[]>().ToArray(),
                    $"Chunk capture timed out after {got}/{ChunkCount} replies.");
            }
            finally { transport.SysExReceived -= Handler; }
        }
        finally { _gate.Release(); }
    }

    private async Task<SysExTransferResult> ReceiveFullDumpAsync(TimeSpan timeout, FirmwareFamily family, bool requestUpload, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? _, byte[] data) { if (data.Length == FcbSysExCodec.DumpLength) tcs.TrySetResult(data); }
        transport.SysExReceived += Handler;
        try
        {
            if (requestUpload) await transport.SendSysExAsync(FullDumpRequest, ct);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            var data = await tcs.Task.WaitAsync(timeoutCts.Token);
            _ = FcbSysExCodec.Parse(data, family);
            _lastReceived = data.ToArray();
            return new(true, data, $"Received and validated a complete {data.Length:N0}-byte FCB1010 dump.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, null,
                $"No complete 2,352-byte FCB1010 dump was received within {timeout.TotalSeconds:0} seconds. UnO supports automatic upload via 4F; stock firmware requires GLOBAL CONFIG → SYSEX SEND (switch 6).");
        }
        finally { transport.SysExReceived -= Handler; }
    }

    public void Dispose() { _gate.Dispose(); transport.Dispose(); }
}

public sealed record FirmwareProbeResult(bool Completed, FirmwareFamily Family, string? Signature, string Message);
public sealed record ChunkCaptureResult(bool Completed, IReadOnlyList<byte[]> Chunks, string Message);
