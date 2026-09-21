namespace FCB1010.App.Controls;

/// <summary>Factory silk-screen labels for each FCB1010 switch (verified against device artwork + manual).</summary>
public static class FcbHardwareLabels
{
    public readonly record struct SwitchFace(
        string Identity,       // "1".."10", "UP", "DOWN"
        string Primary,        // e.g. "CNT 1", "PROG CHG 3", "ENTER"
        string? Secondary,     // e.g. "SYSEX SEND" — reverse pill when SecondaryBoxed
        bool SecondaryBoxed,
        bool IdentityBoxed);   // UP/DOWN white outline box on the hardware

    /// <summary>
    /// Pedal faces 1–10. Primary = preset MIDI-function silk; Secondary = global-config action silk.
    /// </summary>
    public static SwitchFace ForPedal(int number) => number switch
    {
        1 => new("1", "PROG CHG 1", "SWITCH 1", true, false),
        2 => new("2", "PROG CHG 2", "SWITCH 2", true, false),
        3 => new("3", "PROG CHG 3", null, false, false),
        4 => new("4", "PROG CHG 4", null, false, false),
        5 => new("5", "PROG CHG 5", "COPY PRESET", true, false),
        6 => new("6", "CNT 1", "SYSEX SEND", true, false),
        7 => new("7", "CNT 2", "SYSEX RCV", true, false),
        8 => new("8", "EXP A", "MERGE", true, false),
        9 => new("9", "EXP B", "RUNNING ST.", true, false),
        10 => new("10", "NOTE", "DIRECT SELECT", true, false),
        _ => new(number.ToString(), "", null, false, false),
    };

    // Hardware: boxed UP + ENTER below; boxed DOWN + ESCAPE below.
    public static SwitchFace Up { get; } = new("UP", "", "ENTER", false, true);
    public static SwitchFace Down { get; } = new("DOWN", "", "ESCAPE", false, true);
}

public enum FootswitchLedState
{
    Off,
    On,
    ActivePreset,
    ActiveStomp,
    Pressed,
}
