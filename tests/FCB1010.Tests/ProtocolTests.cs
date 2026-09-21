using FCB1010.Core;
using FCB1010.Midi;

namespace FCB1010.Tests;

public sealed class ProtocolTests
{
    private static byte[] Fixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "stock", "fcbtool-vamp.hex");
        return Convert.FromHexString(File.ReadAllText(path).Trim());
    }

    private static byte[] UnoFixture() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "uno", "FCB1010-UnO-hardware.syx"));

    [Fact]
    public void Verified_stock_fixture_has_expected_envelope() => FcbSysExCodec.ValidateEnvelope(Fixture());

    [Fact]
    public void Parse_serialize_without_edit_is_byte_exact()
    {
        var original = Fixture();
        var parsed = FcbSysExCodec.Parse(original, FirmwareFamily.Stock);
        Assert.Equal(100, parsed.Presets.Count);
        Assert.Equal(original, FcbSysExCodec.Serialize(parsed));
    }

    [Fact]
    public void Hardware_verified_Uno_dump_decodes_and_reencodes_byte_exact()
    {
        var original = UnoFixture();
        var decoded = FcbSysExCodec.Decode(original);
        Assert.Equal(FcbSysExCodec.DecodedLength, decoded.Length);
        Assert.Equal(original, FcbSysExCodec.Encode(decoded));
        var parsed = FcbSysExCodec.Parse(original, FirmwareFamily.UnO);
        Assert.Equal(original, FcbSysExCodec.Serialize(parsed));
        Assert.Equal([1,1,1,1,1,1,1,1,1,1], parsed.Global.MidiChannels);
        Assert.True(parsed.Global.RunningStatus);
        Assert.True(parsed.Global.DirectSelect);
    }

    [Fact]
    public void Decoded_field_edit_changes_only_expected_memory_byte()
    {
        var config = FcbSysExCodec.Parse(UnoFixture(), FirmwareFamily.UnO);
        var before = FcbSysExCodec.Decode(FcbSysExCodec.Serialize(config));
        config.GetPreset(0, 1).ProgramChanges[0].Program = 99;
        var after = FcbSysExCodec.Decode(FcbSysExCodec.Serialize(config));
        var changed = before.Zip(after).Select((pair, index) => (pair, index)).Where(x => x.pair.First != x.pair.Second).ToArray();
        Assert.Single(changed); Assert.Equal(0, changed[0].index); Assert.Equal(99, after[0] & 0x7F);
    }

    [Fact]
    public void Preset_indexing_is_ten_banks_of_ten()
    {
        var config = new FcbConfiguration();
        Assert.Equal(0, config.GetPreset(0, 1).Index);
        Assert.Equal(9, config.GetPreset(0, 10).Index);
        Assert.Equal(99, config.GetPreset(9, 10).Index);
    }

    [Fact]
    public void Known_fields_survive_round_trip()
    {
        var config = FcbSysExCodec.Parse(Fixture());
        var p = config.GetPreset(3, 7);
        p.ProgramChanges[4].Enabled = true; p.ProgramChanges[4].Program = 126;
        p.ControlChanges[0].Enabled = true; p.ControlChanges[0].Controller = 64; p.ControlChanges[0].Value = 127;
        p.ExpressionA.Minimum = 12; p.ExpressionA.Maximum = 118; p.Note.Enabled = true; p.Note.Note = 61;
        config.Global.MidiChannels = [1,2,3,4,5,6,7,8,9,16];
        var reparsed = FcbSysExCodec.Parse(FcbSysExCodec.Serialize(config)); var q = reparsed.GetPreset(3, 7);
        Assert.True(q.ProgramChanges[4].Enabled); Assert.Equal(126, q.ProgramChanges[4].Program);
        Assert.Equal(64, q.ControlChanges[0].Controller); Assert.Equal(127, q.ControlChanges[0].Value);
        Assert.Equal(12, q.ExpressionA.Minimum); Assert.Equal(118, q.ExpressionA.Maximum); Assert.Equal(61, q.Note.Note);
        Assert.Equal(Enumerable.Range(1, 9).Append(16), reparsed.Global.MidiChannels);
    }

    [Fact]
    public void Malformed_and_truncated_messages_are_rejected_actionably()
    {
        var truncated = Fixture()[..1000];
        var ex = Assert.Throws<SysExFormatException>(() => FcbSysExCodec.Parse(truncated));
        Assert.Contains("observed 1000 bytes", ex.Message);
        var bad = Fixture(); bad[^1] = 0;
        Assert.Contains("final F7", Assert.Throws<SysExFormatException>(() => FcbSysExCodec.Parse(bad)).Message);
    }

    [Fact]
    public void Validator_rejects_out_of_range_values_and_expression_order()
    {
        var config = new FcbConfiguration(); config.Presets[0].ProgramChanges[0].Program = 128; config.Presets[0].ExpressionA.Minimum = 100; config.Presets[0].ExpressionA.Maximum = 20;
        var issues = ConfigurationValidator.Validate(config);
        Assert.Contains(issues, i => i.Path.EndsWith("PC1")); Assert.Contains(issues, i => i.Message.Contains("Minimum cannot exceed"));
    }

    [Fact]
    public void Assembler_handles_fragmentation_and_ignores_noise()
    {
        var assembler = new SysExAssembler(); var now = DateTimeOffset.UtcNow;
        Assert.Null(assembler.Feed([0x90, 0x40, 0x7F, 0xF0, 0x01], now));
        Assert.Null(assembler.Feed([0x02, 0x03], now));
        Assert.Equal(new byte[] { 0xF0, 0x01, 0x02, 0x03, 0xF7 }, assembler.Feed([0xF7], now));
    }

    [Fact]
    public void Assembler_reports_partial_byte_count_on_timeout()
    {
        var assembler = new SysExAssembler(); var now = DateTimeOffset.UtcNow; assembler.Feed([0xF0, 0x01, 0x02], now);
        var message = assembler.CheckTimeout(now.AddSeconds(6), TimeSpan.FromSeconds(5));
        Assert.Contains("only 3 bytes", message);
    }

    [Fact]
    public void Dump_compare_reports_offset_xor_and_mapping()
    {
        var a = Fixture(); var b = a.ToArray(); b[2311] ^= 1;
        var difference = Assert.Single(DumpComparer.Compare(a, b));
        Assert.Equal(2311, difference.Offset); Assert.Equal(1, difference.Xor); Assert.Equal("PC1 channel", difference.LikelyField);
    }

    [Fact]
    public void Decoded_memory_lab_describes_preset_and_flags()
    {
        var decoded = FcbSysExCodec.Decode(UnoFixture());
        var row = Assert.Single(DecodedMemoryLab.Inspect(decoded, 0, 1));
        Assert.Contains("PC1", row.Meaning);
        Assert.Equal(DecodedMemoryLab.VerificationStatus.Confirmed, row.Status);
        Assert.Contains("UNVERIFIED", DecodedMemoryLab.DescribeDecodedOffset(1600));
    }

    [Fact]
    public void Unknown_reserved_bytes_survive_single_field_edit()
    {
        var original = UnoFixture();
        var before = FcbSysExCodec.Decode(original);
        Assert.All(before.AsSpan(1600, 416).ToArray(), b => Assert.Equal(0xFF, b));
        var config = FcbSysExCodec.Parse(original, FirmwareFamily.UnO);
        config.GetPreset(1, 2).ControlChanges[1].Controller = 77;
        var after = FcbSysExCodec.Decode(FcbSysExCodec.Serialize(config));
        Assert.True(before.AsSpan(1600, 416).SequenceEqual(after.AsSpan(1600, 416)));
    }

    [Fact]
    public void Live_preview_emits_documented_message_order()
    {
        var config = FcbSysExCodec.Parse(UnoFixture(), FirmwareFamily.UnO);
        var preset = config.GetPreset(0, 1);
        preset.ProgramChanges[0].Enabled = true;
        preset.ProgramChanges[4].Enabled = true;
        preset.ControlChanges[0].Enabled = true;
        preset.Note.Enabled = true;
        var events = LiveMidiPreview.BuildPressEvents(config, preset);
        Assert.Contains(events, e => e is Melanchall.DryWetMidi.Core.ProgramChangeEvent);
        Assert.Contains(events, e => e is Melanchall.DryWetMidi.Core.ControlChangeEvent);
        Assert.Contains(events, e => e is Melanchall.DryWetMidi.Core.NoteOnEvent);
    }
}
