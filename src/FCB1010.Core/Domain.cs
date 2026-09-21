using System.Text.Json;
using System.Text.Json.Serialization;

namespace FCB1010.Core;

public enum FirmwareFamily { Stock, UnO, Unknown }

public sealed class FcbConfiguration
{
    public FirmwareFamily Firmware { get; set; } = FirmwareFamily.Unknown;
    public string? FirmwareVersion { get; set; }
    public List<FcbPreset> Presets { get; set; } = Enumerable.Range(0, 100).Select(FcbPreset.CreateDefault).ToList();
    public GlobalConfiguration Global { get; set; } = new();
    public Dictionary<int, string> PresetNames { get; set; } = [];
    public Dictionary<int, string> PresetNotes { get; set; } = [];
    [JsonIgnore] public byte[]? SourceSysEx { get; set; }
    public string? SourceDescription { get; set; }

    /// <summary>Clone editor state without dropping the raw dump hidden from project JSON.</summary>
    public FcbConfiguration DeepClone()
    {
        var clone = JsonSerializer.Deserialize<FcbConfiguration>(JsonSerializer.Serialize(this))
            ?? throw new InvalidOperationException("Could not clone FCB1010 configuration.");
        clone.SourceSysEx = SourceSysEx?.ToArray();
        return clone;
    }

    public FcbPreset GetPreset(int bank, int footswitch)
    {
        if (bank is < 0 or > 9 || footswitch is < 1 or > 10) throw new ArgumentOutOfRangeException();
        return Presets[bank * 10 + footswitch - 1];
    }
}

public sealed class FcbPreset
{
    public int Index { get; set; }
    public List<ProgramChangeAction> ProgramChanges { get; set; } = [];
    public List<ControlChangeAction> ControlChanges { get; set; } = [];
    public NoteAction Note { get; set; } = new();
    public ExpressionAction ExpressionA { get; set; } = new() { Controller = 27 };
    public ExpressionAction ExpressionB { get; set; } = new() { Controller = 7 };
    public bool Switch1Closed { get; set; }
    public bool Switch2Closed { get; set; }

    public int Bank => Index / 10;
    public int Footswitch => Index % 10 + 1;

    public static FcbPreset CreateDefault(int index) => new()
    {
        Index = index,
        ProgramChanges = Enumerable.Range(0, 5).Select(i => new ProgramChangeAction { Enabled = i == 0, Program = i == 0 ? index : 0 }).ToList(),
        ControlChanges = [new(), new()],
        ExpressionA = new() { Enabled = true, Controller = 27, Minimum = 0, Maximum = 127 },
        ExpressionB = new() { Enabled = true, Controller = 7, Minimum = 0, Maximum = 127 },
        Note = new() { Note = 60 }
    };
}

public sealed class ProgramChangeAction { public bool Enabled { get; set; } public int Program { get; set; } }
public sealed class ControlChangeAction { public bool Enabled { get; set; } public int Controller { get; set; } public int Value { get; set; } public int? AlternateValue { get; set; } }
public sealed class NoteAction { public bool Enabled { get; set; } public int Note { get; set; } = 60; }
public sealed class ExpressionAction { public bool Enabled { get; set; } public int Controller { get; set; } public int Minimum { get; set; } public int Maximum { get; set; } = 127; }

public sealed class GlobalConfiguration
{
    public int[] MidiChannels { get; set; } = Enumerable.Repeat(1, 10).ToArray();
    public bool DirectSelect { get; set; }
    public bool RunningStatus { get; set; } = true;
    public bool MidiMerge { get; set; } = true;
    public bool Switch1Momentary { get; set; }
    public bool Switch2Momentary { get; set; }
    public int ExpressionACalibrationMin { get; set; }
    public int ExpressionACalibrationMax { get; set; } = 127;
    public int ExpressionBCalibrationMin { get; set; }
    public int ExpressionBCalibrationMax { get; set; } = 127;
}

public sealed record ValidationIssue(string Path, string Message, bool IsError = true);

public static class ConfigurationValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(FcbConfiguration config)
    {
        var issues = new List<ValidationIssue>();
        if (config.Presets.Count != 100) issues.Add(new("Presets", $"Expected 100 presets; found {config.Presets.Count}."));
        foreach (var p in config.Presets)
        {
            if (p.ProgramChanges.Count != 5) issues.Add(new($"Preset[{p.Index}].ProgramChanges", "Exactly five Program Change slots are required."));
            if (p.ControlChanges.Count != 2) issues.Add(new($"Preset[{p.Index}].ControlChanges", "Exactly two Control Change slots are required."));
            foreach (var (pc, i) in p.ProgramChanges.Select((v, i) => (v, i))) Range(pc.Program, $"Preset[{p.Index}].PC{i + 1}", issues);
            foreach (var (cc, i) in p.ControlChanges.Select((v, i) => (v, i))) { Range(cc.Controller, $"Preset[{p.Index}].CC{i + 1}.Controller", issues); Range(cc.Value, $"Preset[{p.Index}].CC{i + 1}.Value", issues); if (cc.AlternateValue is int a) Range(a, $"Preset[{p.Index}].CC{i + 1}.AlternateValue", issues); }
            Range(p.Note.Note, $"Preset[{p.Index}].Note", issues);
            ValidateExpression(p.ExpressionA, $"Preset[{p.Index}].ExpressionA", issues);
            ValidateExpression(p.ExpressionB, $"Preset[{p.Index}].ExpressionB", issues);
        }
        if (config.Global.MidiChannels.Length != 10) issues.Add(new("Global.MidiChannels", "Exactly ten global MIDI channels are required."));
        else foreach (var (channel, i) in config.Global.MidiChannels.Select((v, i) => (v, i))) if (channel is < 1 or > 16) issues.Add(new($"Global.MidiChannels[{i}]", "MIDI channel must be 1-16."));
        return issues;
    }

    private static void Range(int value, string path, List<ValidationIssue> issues) { if (value is < 0 or > 127) issues.Add(new(path, $"Value {value} is outside MIDI range 0-127.")); }
    private static void ValidateExpression(ExpressionAction e, string path, List<ValidationIssue> issues) { Range(e.Controller, path + ".Controller", issues); Range(e.Minimum, path + ".Minimum", issues); Range(e.Maximum, path + ".Maximum", issues); if (e.Minimum > e.Maximum) issues.Add(new(path, "Minimum cannot exceed maximum.")); }
}
