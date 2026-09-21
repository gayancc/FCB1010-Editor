using System.Text;

namespace FCB1010.Core;

/// <summary>Decoded-memory inspector and differential helpers for protocol lab work.</summary>
public static class DecodedMemoryLab
{
    public const int PresetRegionEnd = 1600;   // 0x640
    public const int ChannelBase = 2016;       // 0x7E0
    public const int FlagsRelay = 2032;
    public const int FlagsGlobal = 2033;
    public const int CalibrationBase = 2044;

    public sealed record ByteInsight(int Offset, byte Value, string Hex, string Binary, string Dec, string Meaning, VerificationStatus Status);
    public sealed record MemoryDiff(int Offset, byte OldValue, byte NewValue, byte Xor, string Meaning, string ChangedBits);

    public enum VerificationStatus { Confirmed, Observed, Unverified, Unknown }

    public static IReadOnlyList<ByteInsight> Inspect(ReadOnlySpan<byte> decoded, int start = 0, int count = -1)
    {
        if (decoded.Length != FcbSysExCodec.DecodedLength)
            throw new ArgumentException($"Expected {FcbSysExCodec.DecodedLength} decoded bytes.");
        var end = count < 0 ? decoded.Length : Math.Min(decoded.Length, start + count);
        var list = new List<ByteInsight>(end - start);
        for (var i = start; i < end; i++)
        {
            var v = decoded[i];
            list.Add(new ByteInsight(i, v, v.ToString("X2"), Convert.ToString(v, 2).PadLeft(8, '0'), v.ToString(), DescribeDecodedOffset(i, v), StatusFor(i)));
        }
        return list;
    }

    public static IReadOnlyList<MemoryDiff> Diff(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var n = Math.Max(a.Length, b.Length);
        var list = new List<MemoryDiff>();
        for (var i = 0; i < n; i++)
        {
            var av = i < a.Length ? a[i] : (byte)0;
            var bv = i < b.Length ? b[i] : (byte)0;
            if (av == bv) continue;
            var xor = (byte)(av ^ bv);
            list.Add(new MemoryDiff(i, av, bv, xor, DescribeDecodedOffset(i, bv), BitsChanged(xor)));
        }
        return list;
    }

    public static string DescribeDecodedOffset(int offset, byte value = 0)
    {
        if (offset < PresetRegionEnd)
        {
            var preset = offset / 16;
            var rel = offset % 16;
            var bank = preset / 10;
            var sw = preset % 10 + 1;
            var on = (value & 0x80) == 0;
            var field = rel switch
            {
                <= 4 => $"PC{rel + 1} ({(on ? "on" : "off")} prog {value & 0x7F})",
                5 => $"CC1 controller ({(on ? "on" : "off")} #{value & 0x7F})",
                6 => $"CC1 value {value & 0x7F}; SW1={((value & 0x80) != 0)}",
                7 => $"CC2 controller ({(on ? "on" : "off")} #{value & 0x7F})",
                8 => $"CC2 value {value & 0x7F}; SW2={((value & 0x80) != 0)}",
                9 => $"ExpA controller ({(on ? "on" : "off")} #{value & 0x7F})",
                10 => $"ExpA min {value & 0x7F}",
                11 => $"ExpA max {value & 0x7F}",
                12 => $"ExpB controller ({(on ? "on" : "off")} #{value & 0x7F})",
                13 => $"ExpB min {value & 0x7F}",
                14 => $"ExpB max {value & 0x7F}",
                15 => $"Note ({(on ? "on" : "off")} #{value & 0x7F})",
                _ => "preset"
            };
            return $"Preset {bank:00}/{sw} [{preset}] {field}";
        }

        if (offset is >= ChannelBase and < ChannelBase + 10)
        {
            var labels = new[] { "PC1", "PC2", "PC3", "PC4", "PC5", "CC1", "CC2", "ExpA", "ExpB", "Note" };
            return $"{labels[offset - ChannelBase]} MIDI channel (stored {value & 0x0F} = UI {(value & 0x0F) + 1})";
        }

        return offset switch
        {
            FlagsRelay => $"Relay global flags (SW1 mom bit7, SW2 mom bit6) raw=0x{value:X2}",
            FlagsGlobal => $"Global flags (DS bit1, RS bit2, Merge bit4; bit3 UNVERIFIED) raw=0x{value:X2}",
            >= CalibrationBase and <= CalibrationBase + 3 => $"Expression calibration byte {offset - CalibrationBase}",
            >= PresetRegionEnd and < ChannelBase => "Reserved / likely UnO extension (UNVERIFIED)",
            _ => "Trailing / unknown decoded byte"
        };
    }

    public static VerificationStatus StatusFor(int offset) => offset switch
    {
        < PresetRegionEnd => VerificationStatus.Confirmed,
        >= ChannelBase and < ChannelBase + 10 => VerificationStatus.Confirmed,
        FlagsGlobal => VerificationStatus.Observed,
        FlagsRelay => VerificationStatus.Observed,
        >= CalibrationBase and <= CalibrationBase + 3 => VerificationStatus.Observed,
        >= PresetRegionEnd and < ChannelBase => VerificationStatus.Unverified,
        _ => VerificationStatus.Unknown
    };

    public static string FormatDiffReport(IReadOnlyList<MemoryDiff> diffs)
    {
        if (diffs.Count == 0) return "No differences.";
        var sb = new StringBuilder();
        sb.AppendLine($"Changed bytes: {diffs.Count}");
        foreach (var d in diffs.Take(200))
            sb.AppendLine($"  @{d.Offset:X4}  {d.OldValue:X2} → {d.NewValue:X2}  xor={d.Xor:X2} bits[{d.ChangedBits}]  {d.Meaning}");
        if (diffs.Count > 200) sb.AppendLine($"  … {diffs.Count - 200} more");
        return sb.ToString();
    }

    private static string BitsChanged(byte xor)
    {
        var parts = new List<string>(8);
        for (var i = 0; i < 8; i++) if (((xor >> i) & 1) != 0) parts.Add(i.ToString());
        return string.Join(',', parts);
    }
}
