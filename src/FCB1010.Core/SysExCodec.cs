namespace FCB1010.Core;

public sealed class SysExFormatException(string message) : Exception(message);

public static class FcbSysExCodec
{
    public const int DumpLength = 2352;
    public const int DecodedLength = 2051;
    public const int MemoryLength = 2048;
    public static readonly byte[] Header = [0xF0, 0x00, 0x20, 0x32, 0x01, 0x0C, 0x0F];

    public static FcbConfiguration Parse(ReadOnlySpan<byte> data, FirmwareFamily firmware = FirmwareFamily.Unknown)
    {
        ValidateEnvelope(data);
        var memory = Decode(data);
        var config = new FcbConfiguration { Firmware = firmware, SourceSysEx = data.ToArray() };

        foreach (var preset in config.Presets)
        {
            var offset = preset.Index * 16;
            for (var i = 0; i < 5; i++) { preset.ProgramChanges[i].Enabled = IsEnabled(memory[offset + i]); preset.ProgramChanges[i].Program = Value(memory[offset + i]); }
            preset.ControlChanges[0].Enabled = IsEnabled(memory[offset + 5]); preset.ControlChanges[0].Controller = Value(memory[offset + 5]);
            preset.ControlChanges[0].Value = Value(memory[offset + 6]); preset.Switch1Closed = HasMsb(memory[offset + 6]);
            preset.ControlChanges[1].Enabled = IsEnabled(memory[offset + 7]); preset.ControlChanges[1].Controller = Value(memory[offset + 7]);
            preset.ControlChanges[1].Value = Value(memory[offset + 8]); preset.Switch2Closed = HasMsb(memory[offset + 8]);
            ReadExpression(memory, offset + 9, preset.ExpressionA); ReadExpression(memory, offset + 12, preset.ExpressionB);
            preset.Note.Enabled = IsEnabled(memory[offset + 15]); preset.Note.Note = Value(memory[offset + 15]);
        }

        for (var i = 0; i < 10; i++) config.Global.MidiChannels[i] = (memory[2016 + i] & 0x0F) + 1;
        config.Global.Switch1Momentary = (memory[2032] & 0x80) != 0;
        config.Global.Switch2Momentary = (memory[2032] & 0x40) != 0;
        config.Global.MidiMerge = (memory[2033] & 0x10) != 0;
        config.Global.RunningStatus = (memory[2033] & 0x04) != 0;
        config.Global.DirectSelect = (memory[2033] & 0x02) != 0;
        config.Global.ExpressionACalibrationMin = memory[2044] - 8; config.Global.ExpressionACalibrationMax = memory[2045] + 5;
        config.Global.ExpressionBCalibrationMin = memory[2046] - 8; config.Global.ExpressionBCalibrationMax = memory[2047] + 5;
        return config;
    }

    public static byte[] Serialize(FcbConfiguration config)
    {
        var issues = ConfigurationValidator.Validate(config).Where(i => i.IsError).ToArray();
        if (issues.Length > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, issues.Select(i => $"{i.Path}: {i.Message}")));
        var memory = config.SourceSysEx is { Length: DumpLength } raw ? Decode(raw) : CreateCanonicalMemory();

        foreach (var preset in config.Presets)
        {
            var offset = preset.Index * 16;
            for (var i = 0; i < 5; i++) memory[offset + i] = Command(preset.ProgramChanges[i].Enabled, preset.ProgramChanges[i].Program);
            memory[offset + 5] = Command(preset.ControlChanges[0].Enabled, preset.ControlChanges[0].Controller);
            memory[offset + 6] = ValueWithFlag(preset.ControlChanges[0].Value, preset.Switch1Closed);
            memory[offset + 7] = Command(preset.ControlChanges[1].Enabled, preset.ControlChanges[1].Controller);
            memory[offset + 8] = ValueWithFlag(preset.ControlChanges[1].Value, preset.Switch2Closed);
            WriteExpression(memory, offset + 9, preset.ExpressionA); WriteExpression(memory, offset + 12, preset.ExpressionB);
            memory[offset + 15] = Command(preset.Note.Enabled, preset.Note.Note);
        }

        for (var i = 0; i < 10; i++) memory[2016 + i] = (byte)(config.Global.MidiChannels[i] - 1);
        SetBit(memory, 2032, 0x80, config.Global.Switch1Momentary); SetBit(memory, 2032, 0x40, config.Global.Switch2Momentary);
        SetBit(memory, 2033, 0x10, config.Global.MidiMerge); SetBit(memory, 2033, 0x04, config.Global.RunningStatus); SetBit(memory, 2033, 0x02, config.Global.DirectSelect);
        memory[2044] = checked((byte)(config.Global.ExpressionACalibrationMin + 8)); memory[2045] = checked((byte)(config.Global.ExpressionACalibrationMax - 5));
        memory[2046] = checked((byte)(config.Global.ExpressionBCalibrationMin + 8)); memory[2047] = checked((byte)(config.Global.ExpressionBCalibrationMax - 5));
        return Encode(memory);
    }

    public static byte[] Decode(ReadOnlySpan<byte> data)
    {
        ValidateEnvelope(data);
        var decoded = new byte[DecodedLength];
        for (var group = 0; group < 293; group++)
        {
            var source = Header.Length + group * 8; var msbs = data[source + 7];
            for (var i = 0; i < 7; i++) decoded[group * 7 + i] = (byte)(data[source + i] | (((msbs >> i) & 1) << 7));
        }
        return decoded;
    }

    public static byte[] Encode(ReadOnlySpan<byte> decoded)
    {
        if (decoded.Length != DecodedLength) throw new ArgumentException($"Expected {DecodedLength} decoded bytes.", nameof(decoded));
        var data = new byte[DumpLength]; Header.CopyTo(data, 0);
        for (var group = 0; group < 293; group++)
        {
            var target = Header.Length + group * 8; byte msbs = 0;
            for (var i = 0; i < 7; i++) { var value = decoded[group * 7 + i]; data[target + i] = (byte)(value & 0x7F); msbs |= (byte)(((value >> 7) & 1) << i); }
            data[target + 7] = msbs;
        }
        data[^1] = 0xF7; return data;
    }

    public static void ValidateEnvelope(ReadOnlySpan<byte> data)
    {
        if (data.Length != DumpLength) throw new SysExFormatException($"Expected a 2,352-byte complete dump; observed {data.Length} bytes.");
        if (!data[..Header.Length].SequenceEqual(Header)) throw new SysExFormatException("The message does not have the confirmed FCB1010 dump header F0 00 20 32 01 0C 0F.");
        if (data[^1] != 0xF7) throw new SysExFormatException("The dump is truncated or malformed: final F7 is missing.");
        for (var i = 1; i < data.Length - 1; i++) if (data[i] > 0x7F) throw new SysExFormatException($"Invalid MIDI data byte 0x{data[i]:X2} at offset {i}; SysEx payload must be 7-bit.");
    }

    private static byte[] CreateCanonicalMemory() { var memory = new byte[DecodedLength]; Array.Fill(memory, (byte)0xFF, 1600, 416); memory[2044] = 8; memory[2045] = 122; memory[2046] = 8; memory[2047] = 122; return memory; }
    private static bool IsEnabled(byte encoded) => (encoded & 0x80) == 0;
    private static bool HasMsb(byte encoded) => (encoded & 0x80) != 0;
    private static int Value(byte encoded) => encoded & 0x7F;
    private static void ReadExpression(ReadOnlySpan<byte> memory, int offset, ExpressionAction action) { action.Enabled = IsEnabled(memory[offset]); action.Controller = Value(memory[offset]); action.Minimum = Value(memory[offset + 1]); action.Maximum = Value(memory[offset + 2]); }
    private static void WriteExpression(Span<byte> memory, int offset, ExpressionAction action) { memory[offset] = Command(action.Enabled, action.Controller); memory[offset + 1] = (byte)action.Minimum; memory[offset + 2] = (byte)action.Maximum; }
    private static byte Command(bool enabled, int value) => (byte)((value & 0x7F) | (enabled ? 0 : 0x80));
    private static byte ValueWithFlag(int value, bool flag) => (byte)((value & 0x7F) | (flag ? 0x80 : 0));
    private static void SetBit(Span<byte> data, int offset, byte mask, bool value) => data[offset] = value ? (byte)(data[offset] | mask) : (byte)(data[offset] & ~mask);
}
