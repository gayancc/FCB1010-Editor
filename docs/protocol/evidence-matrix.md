# Protocol evidence matrix

Status vocabulary:

| Label | Meaning |
|---|---|
| CONFIRMED | Corroborated by official Behringer SysEx sheet and ≥1 independent open-source implementation |
| HARDWARE | Verified on the physical regular-UnO FCB1010 in this repository (CME WIDI) |
| OBSERVED | Seen in fixtures/captures; semantics still partially uncertain |
| UNVERIFIED | Documented behaviorally, byte mapping not proven — do not invent writes |
| STOCK-ONLY / UNO-ONLY | Firmware-specific |

## Transport / commands

| FIELD / BEHAVIOR | SOURCE | STOCK | UNO | CONFIDENCE | HARDWARE | NOTES |
|---|---|---|---|---|---|---|
| Dump header `F0 00 20 32 01 0C 0F` | Behringer SysEx PDF; riban; fcbtool; captures | yes | yes | high | yes | Byte 4 (`01`) is global/device channel in fixtures |
| Dump length 2352 | UnO ControlCenter manual; both OSS projects | yes | yes | high | yes | |
| 7+1 MSB packing | Behringer SysEx PDF | yes | yes | high | yes | 293 groups → 2051 decoded bytes |
| Firmware probe `… 40 F7` | Hardware scan + UnO docs | ? | yes | high | yes | Response `09 0E` on tested UnO unit |
| Current patch `… 45 F7` | Hardware | ? | yes | high | yes | Response `… 00 pp F7` |
| Full upload request `… 4F F7` | Hardware + UnO | no* | yes | high | yes | *Stock requires manual SYSEX SEND (switch 6) |
| Chunk upload req `50..5F` | UnO changelog 1.0.3; hardware | no | yes | high | yes | Response cmd `10..1F`, 160 bytes each |
| Chunk download write `10..1F` | UnO ControlCenter manual | no | yes | medium | partial | Framing verified; paced write path implemented |
| Write ACK after dump | UnO changelog 1.0.3 | no | yes | medium | pending | Wait-for-ACK path implemented; exact ACK bytes still recorded per session |
| Full write = 2352-byte dump | Hardware round-trip | yes | yes | high | yes | Read-back byte-identical after PC1 edit |

## Preset memory (decoded)

| FIELD / BEHAVIOR | SOURCE | STOCK | UNO | CONFIDENCE | HARDWARE | NOTES |
|---|---|---|---|---|---|---|
| 100 presets × 16 bytes @ `0x000..0x63F` | Behringer PDF | yes | yes | high | yes | |
| PC1–PC5 program @ +0..+4 | Behringer PDF; OSS | yes | yes | high | yes | Bit7 disabled |
| CC1 controller @ +5 | Behringer PDF | yes | yes | high | yes | Bit7 disabled |
| CC1 value @ +6 low7; SW1 @ +6 bit7 | Behringer PDF | yes | yes | high | yes | |
| CC2 controller @ +7 | Behringer PDF | yes | yes | high | yes | |
| CC2 value @ +8 low7; SW2 @ +8 bit7 | Behringer PDF | yes | yes | high | yes | |
| Exp A ctrl/min/max @ +9..+B | Behringer PDF | yes | yes | high | yes | |
| Exp B ctrl/min/max @ +C..+E | Behringer PDF | yes | yes | high | yes | |
| Note @ +F | Behringer PDF | yes | yes | high | yes | Bit7 disabled; NoteOff = vel 0 (ControlCenter manual) |
| Message order PC1–4, CC1, CC2, PC5, Note | ControlCenter manual | yes | yes | medium | no | Runtime order; not storage order |

## Global memory (decoded)

| FIELD / BEHAVIOR | SOURCE | STOCK | UNO | CONFIDENCE | HARDWARE | NOTES |
|---|---|---|---|---|---|---|
| MIDI channels @ `0x7E0..0x7E9` (2016–2025) | Behringer PDF; OSS; hardware | yes | yes | high | yes | Stored 0–15 = UI 1–16; functions PC1–5, CC1–2, ExpA, ExpB, Note |
| Gap byte @ 2026 / transport packing | fixtures | yes | yes | medium | yes | Between CC2 and ExpA channel in packed stream |
| Direct Select bit @ 2033 bit1 (`0x02`) | OSS + hardware | yes | yes | high | yes | Mutually exclusive with stompbox mode per UnO docs |
| Running Status bit @ 2033 bit2 (`0x04`) | OSS + hardware | yes | yes | high | yes | |
| MIDI Merge bit @ 2033 bit4 (`0x10`) | OSS + hardware | yes | yes | high | yes | |
| 2033 bit3 (`0x08`) | hardware dump | ? | ? | low | observed | Set on tested UnO unit; mapping UNVERIFIED |
| Relay momentary flags @ 2032 bits 7/6 | OSS (riban/fcbtool disagree on transport) | ? | ? | medium | observed | Preserve; UI editable with caution |
| Exp calibration @ 2044–2047 | OSS (min+8 / max−5 UI transform) | yes | yes | medium | observed | Raw ADC-ish values; transform from fcbtool lineage |
| Reserved `0x640..0x7DF` (1600–2015) | Behringer PDF silence; UnO “extra memory” | unused FF | UNO extensions | low | observed | All `0xFF` when UnO features unused; **likely UnO stomp/alt storage** |

## Regular UnO behavioral (byte map often UNVERIFIED)

| FIELD / BEHAVIOR | SOURCE | STOCK | UNO | CONFIDENCE | HARDWARE | NOTES |
|---|---|---|---|---|---|---|
| Stompbox mode (5 global stomps) | UnO 1.0.4 guide | no | yes | high (behavior) | no (bytes) | Upper or lower row; 19×5 presets |
| Per-stomp ON/OFF/unchanged per preset | UnO guide; ControlCenter | no | yes | high (behavior) | no (bytes) | Ternary; not in stock map |
| CC1/CC2 alternate value | UnO guide | no | yes | high (behavior) | no (bytes) | Expected in reserved region |
| CC toggle / momentary (alt=1, toggle off) | UnO guide | no | yes | high (behavior) | no (bytes) | |
| Relay / expression tri-state “no change” | ControlCenter | no | yes | medium | no (bytes) | |
| Always-on SysEx receive | UnO changelog | no | yes | high | yes | No GLOBAL CONFIG enable needed |
| Firmware ID byte `0x0E` | Hardware probe | no | yes | high | yes | Heuristic; UI also allows manual family select |

## Deliberately not claimed

- Automatic firmware detection beyond the `40` probe heuristic
- UnO2 protocol (different product)
- Exact UnO reserved-region layout until differential experiments mark fields VERIFIED
- Copying ControlCenter `.lgp` format or proprietary assets
