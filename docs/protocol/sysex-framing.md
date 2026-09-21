# SysEx framing

## Complete dump

```
F0 00 20 32 gc 0C 0F <293 × 8-byte groups> F7
```

| Offset | Bytes | Meaning |
|---:|---|---|
| 0 | `F0` | SysEx start |
| 1–3 | `00 20 32` | Behringer manufacturer ID |
| 4 | `gc` | Global/device channel byte (fixtures use `01`) |
| 5 | `0C` | FCB1010 model ID |
| 6 | `0F` | Full memory dump |
| 7–2350 | 2344 | Packed payload |
| 2351 | `F7` | SysEx end |

Total length: **2352** bytes. Evidence: Behringer SysEx PDF, ControlCenter manual, riban-bw/fcb1010, trafficpest/fcbtool, hardware captures.

## Packing

Each group of 8 transport bytes reconstructs 7 memory bytes:

```
data[0..6]  = memory[i] & 0x7F
data[7].bit(i) = memory[i] bit 7
```

293 groups × 7 = **2051** decoded bytes. The first **2048** bytes are device memory; trailing decoded bytes exist because of packing granularity and must be preserved on round-trip.

## Command messages (regular UnO, hardware-verified)

| Purpose | Request | Typical response |
|---|---|---|
| Identity | `F0 00 20 32 01 0C 40 F7` | 9 bytes, signature includes `0E` on UnO |
| Current patch | `F0 00 20 32 01 0C 45 F7` | `F0 00 20 32 01 0C 00 pp F7` |
| Full upload | `F0 00 20 32 01 0C 4F F7` | Complete 2352-byte dump |
| Chunk upload n=0..15 | `F0 00 20 32 01 0C (50+n) F7` | 160-byte msg, cmd `10+n` |

## Stock firmware note

Stock firmware does **not** reliably answer `4F`. Use Global Configuration → SYSEX SEND (switch 6) to push the dump. UnO answers `4F` without setup.

## Fragmentation

Windows MIDI drivers and BLE/WIDI adapters may deliver SysEx as multiple callbacks. The transport assembler buffers from `F0` until `F7`, rejects unexpected status bytes inside SysEx, and times out with an exact received-byte count.
