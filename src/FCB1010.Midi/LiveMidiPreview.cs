using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using FCB1010.Core;

namespace FCB1010.Midi;

/// <summary>Builds the channel-voice messages a physical pedal press would send (stock/UnO common subset).</summary>
public static class LiveMidiPreview
{
    /// <summary>ControlCenter-documented order: PC1–PC4, CC1, CC2, PC5, NoteOn.</summary>
    public static IReadOnlyList<MidiEvent> BuildPressEvents(FcbConfiguration config, FcbPreset preset)
    {
        var ch = config.Global.MidiChannels;
        var events = new List<MidiEvent>();

        void Pc(int slot)
        {
            var a = preset.ProgramChanges[slot];
            if (!a.Enabled) return;
            events.Add(new ProgramChangeEvent((SevenBitNumber)a.Program) { Channel = Channel(ch[slot]) });
        }

        void Cc(int slot, int channelIndex)
        {
            var a = preset.ControlChanges[slot];
            if (!a.Enabled) return;
            events.Add(new ControlChangeEvent((SevenBitNumber)a.Controller, (SevenBitNumber)a.Value) { Channel = Channel(ch[channelIndex]) });
        }

        Pc(0); Pc(1); Pc(2); Pc(3);
        Cc(0, 5); Cc(1, 6);
        Pc(4);
        if (preset.Note.Enabled)
            events.Add(new NoteOnEvent((SevenBitNumber)preset.Note.Note, (SevenBitNumber)64) { Channel = Channel(ch[9]) });

        return events;
    }

    public static MidiEvent? BuildExpressionEvent(FcbConfiguration config, ExpressionAction exp, int channelIndex, int rawValue0to127)
    {
        if (!exp.Enabled) return null;
        var span = Math.Max(0, exp.Maximum - exp.Minimum);
        var mapped = exp.Minimum + (int)Math.Round(span * (Math.Clamp(rawValue0to127, 0, 127) / 127.0));
        mapped = Math.Clamp(mapped, 0, 127);
        return new ControlChangeEvent((SevenBitNumber)exp.Controller, (SevenBitNumber)mapped)
        {
            Channel = Channel(config.Global.MidiChannels[channelIndex])
        };
    }

    public static MidiEvent? BuildNoteOff(FcbConfiguration config, FcbPreset preset)
    {
        if (!preset.Note.Enabled) return null;
        return new NoteOnEvent((SevenBitNumber)preset.Note.Note, (SevenBitNumber)0)
        {
            Channel = Channel(config.Global.MidiChannels[9])
        };
    }

    private static FourBitNumber Channel(int uiChannel1to16) => (FourBitNumber)(Math.Clamp(uiChannel1to16, 1, 16) - 1);
}
