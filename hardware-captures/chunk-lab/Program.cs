var dir = @"D:\Codes\FCB1010\hardware-captures\chunk-lab";
var full = File.ReadAllBytes(Path.Combine(dir, "full.syx"));
var chunks = Enumerable.Range(0, 16).Select(i => File.ReadAllBytes(Path.Combine(dir, $"chunk-{i:00}.bin"))).ToArray();

// Hypothesis: first payload byte is index*8 address marker
for (var skip = 0; skip <= 4; skip++)
{
    var parts = new List<byte>();
    foreach (var c in chunks)
        parts.AddRange(c.Skip(7 + skip).Take(c.Length - 8 - skip));
    Console.WriteLine($"skip={skip} concatLen={parts.Count}");
    // try as full dump interior replacement
    foreach (var (name, target) in new (string, byte[])[] {
        ("interior", full[7..^1].ToArray()),
        ("afterF0", full[1..^1].ToArray()),
        ("wholeNoF7", full[..^1].ToArray()),
        ("decoded?", Array.Empty<byte>())
    })
    {
        if (target.Length == 0) continue;
        var n = Math.Min(parts.Count, target.Length);
        var match = 0; for (; match < n && parts[match] == target[match]; match++) {}
        Console.WriteLine($"  vs {name}({target.Length}): prefixMatch={match}");
    }
}

// Maybe each chunk is itself a mini packed sysex of 128 decoded bytes?
// Or: chop full 2352 into 16*147, then wrap each 147 with F0 00 20 32 01 0C (10+i) and pad to 160
for (var i = 0; i < 16; i++)
{
    var slice = full.AsSpan(i * 147, Math.Min(147, 2352 - i * 147));
    var pay = chunks[i].AsSpan(7, 152);
    // does pay start with slice?
    var m = 0; while (m < slice.Length && m < pay.Length && slice[m] == pay[m]) m++;
    // or pay[1..] starts with slice?
    var m2 = 0; if (pay.Length > 1) while (m2 < slice.Length && m2 < pay.Length - 1 && slice[m2] == pay[m2 + 1]) m2++;
    Console.WriteLine($"slice147 chunk{i}: matchAt0={m} matchAt1={m2} slice0={slice[0]:X2} pay0={pay[0]:X2} pay1={pay[1]:X2}");
}

// Compare chunk0 payload to full[7..] — already know 152 match
// What is full[159]?
Console.WriteLine($"full[155..170]={Convert.ToHexString(full.AsSpan(155, 16))}");
Console.WriteLine($"chunk0 tail={Convert.ToHexString(chunks[0].AsSpan(140, 20))}");
Console.WriteLine($"chunk1 head={Convert.ToHexString(chunks[1].AsSpan(0, 24))}");

// Diff chunk0 payload vs chunk1 payload
var a = chunks[0].AsSpan(7, 152);
var b = chunks[1].AsSpan(7, 152);
var diffs = new List<int>();
for (var i = 0; i < 152; i++) if (a[i] != b[i]) diffs.Add(i);
Console.WriteLine($"chunk0 vs chunk1 differing indexes (first 20): {string.Join(',', diffs.Take(20))} count={diffs.Count}");
