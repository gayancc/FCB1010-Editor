using FCB1010.Core;

namespace FCB1010.App.Services;

public sealed record ConfigChange(string Id, string Scope, string Field, string From, string To, Action<FcbConfiguration> Revert);

public static class ConfigDiff
{
    private static readonly string[] ChannelLabels =
        ["PC1", "PC2", "PC3", "PC4", "PC5", "CC1", "CC2", "EXP A", "EXP B", "NOTE"];

    public static IReadOnlyList<ConfigChange> Diff(FcbConfiguration editor, FcbConfiguration baseline)
    {
        var changes = new List<ConfigChange>();
        AddGlobalFlag("direct-select", "Direct Select", editor.Global.DirectSelect, baseline.Global.DirectSelect,
            (cfg, value) => cfg.Global.DirectSelect = value);
        AddGlobalFlag("running-status", "Running Status", editor.Global.RunningStatus, baseline.Global.RunningStatus,
            (cfg, value) => cfg.Global.RunningStatus = value);
        AddGlobalFlag("midi-merge", "MIDI Merge", editor.Global.MidiMerge, baseline.Global.MidiMerge,
            (cfg, value) => cfg.Global.MidiMerge = value);

        void AddGlobalFlag(string id, string field, bool current, bool old, Action<FcbConfiguration, bool> revert)
        {
            if (current != old)
                changes.Add(new(id, "GLOBAL", field, old ? "ON" : "OFF", current ? "ON" : "OFF",
                    cfg => revert(cfg, old)));
        }

        for (var bank = 0; bank < 10; bank++)
        {
            var current = editor.BankNames.GetValueOrDefault(bank, "");
            var old = baseline.BankNames.GetValueOrDefault(bank, "");
            if (current == old) continue;
            var bankIndex = bank;
            changes.Add(new($"bank-{bank}-name", $"BANK {bank:00}", "Bank name", old, current, cfg =>
            {
                if (string.IsNullOrEmpty(old)) cfg.BankNames.Remove(bankIndex);
                else cfg.BankNames[bankIndex] = old;
            }));
        }

        for (var i = 0; i < 10; i++)
        {
            if (editor.Global.MidiChannels[i] == baseline.Global.MidiChannels[i]) continue;
            var idx = i;
            var from = baseline.Global.MidiChannels[i].ToString();
            var to = editor.Global.MidiChannels[i].ToString();
            changes.Add(new($"ch-{i}", "GLOBAL", $"{ChannelLabels[i]} Channel", from, to, cfg => cfg.Global.MidiChannels[idx] = baseline.Global.MidiChannels[idx]));
        }

        for (var index = 0; index < 100; index++)
        {
            var a = editor.Presets[index];
            var b = baseline.Presets[index];
            var bank = index / 10;
            var sw = index % 10 + 1;
            var scope = $"BANK {bank:00} / SWITCH {sw}";
            var idx = index;

            if (editor.PresetNames.GetValueOrDefault(index) != baseline.PresetNames.GetValueOrDefault(index))
            {
                var from = baseline.PresetNames.GetValueOrDefault(index, "");
                var to = editor.PresetNames.GetValueOrDefault(index, "");
                changes.Add(new($"p{index}-name", scope, "Name", from, to, cfg =>
                {
                    if (string.IsNullOrEmpty(from)) cfg.PresetNames.Remove(idx);
                    else cfg.PresetNames[idx] = from;
                }));
            }

            if (editor.PresetNotes.GetValueOrDefault(index) != baseline.PresetNotes.GetValueOrDefault(index))
            {
                var from = baseline.PresetNotes.GetValueOrDefault(index, "");
                var to = editor.PresetNotes.GetValueOrDefault(index, "");
                changes.Add(new($"p{index}-note-text", scope, "Project note", from, to, cfg =>
                {
                    if (string.IsNullOrEmpty(from)) cfg.PresetNotes.Remove(idx);
                    else cfg.PresetNotes[idx] = from;
                }));
            }

            for (var slot = 0; slot < 5; slot++)
            {
                var pcA = a.ProgramChanges[slot];
                var pcB = b.ProgramChanges[slot];
                if (pcA.Enabled != pcB.Enabled || pcA.Program != pcB.Program)
                {
                    var s = slot;
                    changes.Add(new($"p{index}-pc{slot}", scope, $"Program {slot + 1}",
                        FormatPc(pcB), FormatPc(pcA),
                        cfg => { cfg.Presets[idx].ProgramChanges[s].Enabled = pcB.Enabled; cfg.Presets[idx].ProgramChanges[s].Program = pcB.Program; }));
                }
            }

            for (var slot = 0; slot < 2; slot++)
            {
                var ccA = a.ControlChanges[slot];
                var ccB = b.ControlChanges[slot];
                if (ccA.Enabled != ccB.Enabled || ccA.Controller != ccB.Controller || ccA.Value != ccB.Value)
                {
                    var s = slot;
                    changes.Add(new($"p{index}-cc{slot}", scope, $"CC {slot + 1}",
                        FormatCc(ccB), FormatCc(ccA),
                        cfg =>
                        {
                            cfg.Presets[idx].ControlChanges[s].Enabled = ccB.Enabled;
                            cfg.Presets[idx].ControlChanges[s].Controller = ccB.Controller;
                            cfg.Presets[idx].ControlChanges[s].Value = ccB.Value;
                        }));
                }
            }

            if (!ExprEqual(a.ExpressionA, b.ExpressionA))
            {
                var copy = CloneExpr(b.ExpressionA);
                changes.Add(new($"p{index}-expa", scope, "Expression A", FormatExpr(b.ExpressionA), FormatExpr(a.ExpressionA),
                    cfg => cfg.Presets[idx].ExpressionA = copy));
            }
            if (!ExprEqual(a.ExpressionB, b.ExpressionB))
            {
                var copy = CloneExpr(b.ExpressionB);
                changes.Add(new($"p{index}-expb", scope, "Expression B", FormatExpr(b.ExpressionB), FormatExpr(a.ExpressionB),
                    cfg => cfg.Presets[idx].ExpressionB = copy));
            }

            if (a.Switch1Closed != b.Switch1Closed || a.Switch2Closed != b.Switch2Closed)
            {
                var s1 = b.Switch1Closed; var s2 = b.Switch2Closed;
                changes.Add(new($"p{index}-sw", scope, "Relays",
                    $"SW1 {(b.Switch1Closed ? "C" : "O")} / SW2 {(b.Switch2Closed ? "C" : "O")}",
                    $"SW1 {(a.Switch1Closed ? "C" : "O")} / SW2 {(a.Switch2Closed ? "C" : "O")}",
                    cfg => { cfg.Presets[idx].Switch1Closed = s1; cfg.Presets[idx].Switch2Closed = s2; }));
            }

            if (a.Note.Enabled != b.Note.Enabled || a.Note.Note != b.Note.Note)
            {
                var enabled = b.Note.Enabled; var note = b.Note.Note;
                changes.Add(new($"p{index}-midi-note", scope, "MIDI Note",
                    b.Note.Enabled ? b.Note.Note.ToString() : "OFF",
                    a.Note.Enabled ? a.Note.Note.ToString() : "OFF",
                    cfg => { cfg.Presets[idx].Note.Enabled = enabled; cfg.Presets[idx].Note.Note = note; }));
            }
        }

        return changes;
    }

    private static string FormatPc(ProgramChangeAction a) => a.Enabled ? $"PC {a.Program:000}" : "OFF";
    private static string FormatCc(ControlChangeAction a) => a.Enabled ? $"CC{a.Controller:00}={a.Value}" : "OFF";
    private static string FormatExpr(ExpressionAction e) => $"CC{e.Controller:00} {e.Minimum}–{e.Maximum}";
    private static bool ExprEqual(ExpressionAction a, ExpressionAction b) =>
        a.Enabled == b.Enabled && a.Controller == b.Controller && a.Minimum == b.Minimum && a.Maximum == b.Maximum;
    private static ExpressionAction CloneExpr(ExpressionAction e) => new()
    {
        Enabled = e.Enabled, Controller = e.Controller, Minimum = e.Minimum, Maximum = e.Maximum,
    };
}
