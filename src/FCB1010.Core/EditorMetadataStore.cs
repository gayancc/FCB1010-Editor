using System.Security.Cryptography;
using System.Text.Json;

namespace FCB1010.Core;

/// <summary>
/// Stores names separately from MIDI data, keyed by the exact verified device dump.
/// A different dump deliberately does not inherit labels from an unrelated setup.
/// </summary>
public sealed class EditorMetadataStore(string directory)
{
    private sealed class Labels
    {
        public Dictionary<int, string> PresetNames { get; set; } = [];
        public Dictionary<int, string> PresetNotes { get; set; } = [];
        public Dictionary<int, string> BankNames { get; set; } = [];
    }

    public async Task SaveAsync(byte[] verifiedDump, FcbConfiguration config, CancellationToken ct = default)
    {
        FcbSysExCodec.ValidateEnvelope(verifiedDump);
        Directory.CreateDirectory(directory);
        var labels = new Labels
        {
            PresetNames = new(config.PresetNames),
            PresetNotes = new(config.PresetNotes),
            BankNames = new(config.BankNames),
        };
        var path = PathFor(verifiedDump);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(labels), ct);
        File.Move(temporary, path, overwrite: true);
    }

    public async Task<bool> LoadAsync(byte[] dump, FcbConfiguration config, CancellationToken ct = default)
    {
        FcbSysExCodec.ValidateEnvelope(dump);
        var path = PathFor(dump);
        if (!File.Exists(path)) return false;
        var labels = JsonSerializer.Deserialize<Labels>(await File.ReadAllTextAsync(path, ct))
            ?? throw new InvalidDataException("The local name record is empty.");
        config.PresetNames = labels.PresetNames.Where(p => p.Key is >= 0 and < 100).ToDictionary();
        config.PresetNotes = labels.PresetNotes.Where(p => p.Key is >= 0 and < 100).ToDictionary();
        config.BankNames = labels.BankNames.Where(p => p.Key is >= 0 and < 10).ToDictionary();
        return true;
    }

    private string PathFor(byte[] dump) =>
        Path.Combine(directory, Convert.ToHexString(SHA256.HashData(dump)) + ".fcbnames.json");
}
