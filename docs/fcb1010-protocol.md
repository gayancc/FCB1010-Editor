# Stock FCB1010 SysEx protocol

Status labels: **confirmed** means corroborated by public documentation and/or the two MIT projects;
**observed** means fixture-tested; **uncertain** means it must not be generated without preservation.

## Transport envelope

| Transport offset | Meaning | Type/range | Firmware | Evidence | Status |
|---:|---|---|---|---|---|
| 0 | SysEx start | `F0` | Stock v2.5 family | Behringer SysEx structure PDF; both projects | confirmed |
| 1-3 | Behringer manufacturer ID | `00 20 32` | Stock | format PDF | confirmed |
| 4 | device/global channel byte | `01` in fixtures | Stock | public dumps/projects | observed; semantics uncertain |
| 5 | FCB1010 model ID | `0C` | Stock | format PDF/projects | confirmed |
| 6 | full memory dump command | `0F` | Stock | both projects/fixtures | observed |
| 7-2350 | 293 packets of 8 transport bytes | seven data bytes + one MSB bit-pack byte | Stock | format PDF | confirmed |
| 2351 | SysEx end | `F7` | Stock | MIDI/format PDF | confirmed |

The fixed dump length observed by both MIT implementations and their fixtures is 2,352 bytes. Each
group transports seven internal eight-bit bytes: the first seven bytes contain low seven bits and the
eighth carries their MSBs. The implementation reconstructs 2,051 decoded bytes (2,048 memory bytes
plus trailing data), edits that model, and repacks it while preserving every unmodelled byte.

## Decoded preset memory

There are 100 presets, 16 decoded bytes each, at decoded memory address `0x000..0x63F`.
For preset index `p` (0-99), base address is `p * 0x10`.

| Relative offset | Meaning | Range | Firmware | Evidence | Status |
|---:|---|---:|---|---|---|
| 0x00-0x04 | Program Change 1-5 program | 0-127 | Stock | format PDF/projects | confirmed |
| 0x05 | CC1 controller | 0-127 | Stock | format PDF/projects | confirmed |
| 0x06 low 7 | CC1 value | 0-127 | Stock | format PDF/projects | confirmed |
| 0x06 bit 7 | relay SW1 state | boolean | Stock | format PDF/projects | confirmed |
| 0x07 | CC2 controller | 0-127 | Stock | format PDF/projects | confirmed |
| 0x08 low 7 | CC2 value | 0-127 | Stock | format PDF/projects | confirmed |
| 0x08 bit 7 | relay SW2 state | boolean | Stock | format PDF/projects | confirmed |
| 0x09-0x0B | Expression A controller/min/max | 0-127 | Stock | format PDF/projects | confirmed |
| 0x0C-0x0E | Expression B controller/min/max | 0-127 | Stock | format PDF/projects | confirmed |
| 0x0F | Note number | 0-127 | Stock | format PDF/projects | confirmed |

Bit 7 of a decoded command byte means disabled. Bit 7 of each decoded CC-value byte is its relay
state; the low seven bits remain the CC value.

## Known decoded offsets for globals

| Decoded offset | Meaning | Stored range | Evidence | Status |
|---:|---|---:|---|---|
| 2016-2025 | PC1-5, CC1-2, Expression A/B, Note channels | 0-15 (= UI 1-16) | public implementation + hardware | confirmed |
| 2032 bits 7/6 | global relay behavior flags | boolean | public implementation + hardware | observed; wording firmware-sensitive |
| 2033 bit 1 | Direct Select | boolean | public implementation + hardware | confirmed |
| 2033 bit 2 | Running Status | boolean | public implementation + hardware | confirmed |
| 2033 bit 4 | MIDI Merge | boolean | public implementation + hardware | confirmed |
| 2044-2047 | Expression A min/max, B min/max calibration (stored min+8/max-5) | 8-bit | public implementation + hardware | observed |

Offsets not listed are **uncertain**. They are never normalized when a source dump exists.

## Runtime behavior

The public ControlCenter manual states the message order is PC1, PC2, PC3, PC4, CC1, CC2, PC5,
NoteOn. Note release is NoteOn velocity zero. MIDI channels are global per function. Bank numbers are
00-09 in regular mode; direct-select addresses presets 00-99 with two key presses.

## Sources

- [trafficpest/fcbtool](https://github.com/trafficpest/fcbtool)
- [riban-bw/fcb1010](https://github.com/riban-bw/fcb1010)
- [FCB1010 SysEx file structure](https://c3.zzounds.com/media/FCB1010_SysEx_Structure-2c6634e4d8b813c58d175d05a0def4d2.pdf)
- [Behringer FCB1010 manual](https://behringer.noise-emission.com/FCB1010/M/EN/download)
- [FCB/UnO ControlCenter manual](https://www.fcb1010.uno/downloads/FCB_UnO_ControlCenter_manual.pdf)
