using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

var input = InputDevice.GetByIndex(0);
var output = OutputDevice.GetByIndex(1);
var chunks = new byte[16][];
var done = new TaskCompletionSource();
input.EventReceived += (_, e) => {
  if (e.Event is not SysExEvent sx) return;
  var raw = new byte[sx.Data.Length + 1];
  raw[0] = 0xF0;
  sx.Data.CopyTo(raw, 1);
  if (raw.Length == 160 && raw[6] >= 0x10 && raw[6] <= 0x1F) {
    var i = raw[6] - 0x10;
    chunks[i] = raw;
    Console.WriteLine($"CHUNK {i} len={raw.Length}");
    if (chunks.All(c => c != null)) done.TrySetResult();
  }
};
input.StartEventsListening();
output.PrepareForEventsSending();
for (int i = 0; i < 16; i++) {
  output.SendEvent(new NormalSysExEvent(new byte[]{0x00,0x20,0x32,0x01,0x0C,(byte)(0x50+i),0xF7}));
  await Task.Delay(50);
}
await done.Task.WaitAsync(TimeSpan.FromSeconds(15));
input.StopEventsListening();
Directory.CreateDirectory(@"hardware-captures\chunk-lab");
for (int i = 0; i < 16; i++) File.WriteAllBytes($@"hardware-captures\chunk-lab\chunk-{i:00}.bin", chunks[i]);
var full = File.ReadAllBytes(@"hardware-captures\chunk-lab\full.syx");
Console.WriteLine($"FULL len={full.Length}");
// Try: concatenate payloads (skip F0..cmd, skip F7) 
var cat = chunks.SelectMany(c => c.Skip(7).Take(c.Length-8)).ToArray();
Console.WriteLine($"CONCAT payload len={cat.Length}");
File.WriteAllBytes(@"hardware-captures\chunk-lab\concat-payload.bin", cat);
// Compare to full[7..^1]
var interior = full[7..^1];
Console.WriteLine($"FULL interior len={interior.Length}");
int match = 0; for (int i=0;i<Math.Min(cat.Length,interior.Length);i++) if (cat[i]==interior[i]) match++; else { Console.WriteLine($"First mismatch @{i}: cat={cat[i]:X2} full={interior[i]:X2}"); break; }
Console.WriteLine($"Prefix match bytes={match}");
// Try: each chunk carries 147 bytes of full dump
var stitch = new byte[2352];
for (int i=0;i<16;i++) {
  var src = chunks[i].AsSpan(7);
  var dst = i * 147;
  var n = Math.Min(147, 2352 - dst);
  src[..n].CopyTo(stitch.AsSpan(dst));
}
Console.WriteLine($"Stitch147 equal={stitch.SequenceEqual(full)} firstDiff={stitch.Zip(full).Select((p,i)=>(p,i)).FirstOrDefault(x=>x.p.First!=x.p.Second).i}");
