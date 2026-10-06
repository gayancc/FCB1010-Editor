using System.Text.Json;

namespace FCB1010.Core;

public sealed class FcbProject
{
    public int FormatVersion { get; set; } = 1;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
    public required FcbConfiguration Configuration { get; set; }
    public string? RawSysExBase64 { get; set; }
}

public static class ProjectPersistence
{
    private const long MaxProjectBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static async Task SaveProjectAsync(string path, FcbConfiguration config, CancellationToken ct = default)
    {
        ThrowIfInvalid(config);
        var project = new FcbProject { Configuration = config, RawSysExBase64 = config.SourceSysEx is null ? null : Convert.ToBase64String(config.SourceSysEx) };
        await AtomicWriteAsync(path, JsonSerializer.Serialize(project, JsonOptions), ct);
    }
    public static async Task<FcbConfiguration> LoadProjectAsync(string path, CancellationToken ct = default)
    {
        if (new FileInfo(path).Length > MaxProjectBytes)
            throw new InvalidDataException("Project file is larger than the 16 MB safety limit.");
        var project = JsonSerializer.Deserialize<FcbProject>(await File.ReadAllTextAsync(path, ct), JsonOptions) ?? throw new InvalidDataException("Project JSON is empty.");
        if (project.FormatVersion != 1) throw new InvalidDataException($"Unsupported project format version {project.FormatVersion}.");
        if (project.Configuration is null) throw new InvalidDataException("Project configuration is missing.");
        if (project.RawSysExBase64 is not null)
        {
            project.Configuration.SourceSysEx = Convert.FromBase64String(project.RawSysExBase64);
            FcbSysExCodec.ValidateEnvelope(project.Configuration.SourceSysEx);
        }
        ThrowIfInvalid(project.Configuration);
        return project.Configuration;
    }
    public static async Task SaveSysExAsync(string path, FcbConfiguration config, CancellationToken ct = default)
        => await AtomicWriteAsync(path, FcbSysExCodec.Serialize(config), ct);
    public static async Task<FcbConfiguration> LoadSysExAsync(string path, FirmwareFamily family = FirmwareFamily.Unknown, CancellationToken ct = default) => FcbSysExCodec.Parse(await File.ReadAllBytesAsync(path, ct), family);

    private static void ThrowIfInvalid(FcbConfiguration config)
    {
        var errors = ConfigurationValidator.Validate(config).Where(issue => issue.IsError).ToArray();
        if (errors.Length > 0)
            throw new InvalidDataException("Invalid FCB1010 configuration:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Take(20).Select(issue => $"{issue.Path}: {issue.Message}")));
    }

    private static async Task AtomicWriteAsync(string path, string content, CancellationToken ct)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(temporary, content, ct);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private static async Task AtomicWriteAsync(string path, byte[] content, CancellationToken ct)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, content, ct);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}

public sealed record DumpDifference(int Offset, byte OldByte, byte NewByte, byte Xor, string? LikelyField);
public static class DumpComparer
{
    public static IReadOnlyList<DumpDifference> Compare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var result = new List<DumpDifference>(); var count = Math.Max(a.Length, b.Length);
        for (var i = 0; i < count; i++) { var av = i < a.Length ? a[i] : (byte)0; var bv = i < b.Length ? b[i] : (byte)0; if (av != bv) result.Add(new(i, av, bv, (byte)(av ^ bv), FieldMap.DescribeTransportOffset(i))); }
        return result;
    }
}

public static class FieldMap
{
    public static string? DescribeTransportOffset(int offset)
    {
        if (offset == 0) return "SysEx F0";
        if (offset is >= 1 and <= 3) return "Behringer ID 00 20 32";
        if (offset == 4) return "Global/device channel byte";
        if (offset == 5) return "Model ID 0C (FCB1010)";
        if (offset == 6) return "Command 0F (full dump)";
        if (offset == 2351) return "SysEx F7";
        // Transport offsets for global channels after 7+1 packing (from riban/fcbtool + fixtures).
        int[] channels = [2311, 2312, 2313, 2314, 2315, 2316, 2317, 2319, 2320, 2321];
        var ci = Array.IndexOf(channels, offset);
        if (ci >= 0)
            return new[] { "PC1 channel", "PC2 channel", "PC3 channel", "PC4 channel", "PC5 channel", "CC1 channel", "CC2 channel", "Expression A channel", "Expression B channel", "Note channel" }[ci];
        if (offset == 2330) return "Direct Select / Running Status / MIDI Merge flags";
        if (offset is >= 2343 and <= 2346) return "Expression pedal calibration";
        if (offset is >= 7 and < 1836) return "Packed preset data (decode for field)";
        if (offset is >= 1836 and < 2311) return "Packed reserved / UnO extension region (UNVERIFIED)";
        return null;
    }

    public static string? DescribeDecodedOffset(int offset) => DecodedMemoryLab.DescribeDecodedOffset(offset);
}
