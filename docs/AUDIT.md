# Repository audit — 2026-09-21

## Stack

Avalonia 12 desktop app (`FCB1010.App`) + `FCB1010.Core` (SysEx/domain) + `FCB1010.Midi` (DryWetMidi) + `FCB1010.HardwareProbe` + xUnit fixtures. Correct choice for a Windows MIDI hardware editor (not a web app).

## What is already implemented

| Area | Status |
|---|---|
| 2352-byte dump parse/encode with 7+1 packing | Working; round-trip byte-exact on stock + UnO fixtures |
| Preset PC1–5, CC1–2, Note, Exp A/B, SW1/SW2 | Mapped to official Behringer layout |
| Global MIDI channels + Direct Select / Running Status / MIDI Merge | Mapped |
| Source-dump preservation of unknown bytes | Working when `SourceSysEx` present |
| MIDI IN/OUT connect, fragmented SysEx assembler | Working |
| Firmware probe `40`, dump request `4F` | Hardware-verified UnO |
| Write + automatic read-back compare | Implemented |
| Avalonia twin pedalboard UI, map, routing, basic diagnostics | Present |
| HardwareProbe CLI | Present |

## Proven to work (this session / prior hardware day)

- Windows lists `FCB1010 IN` / `FCB1010 OUT` (CME WIDI)
- Probe `40` → `09 0E` (regular UnO)
- Current patch `45` → patch byte
- Full dump `4F` → 2352-byte validated `.syx`
- Chunk request `50` → 160-byte response cmd `10`
- Unit tests: 11/11 passing (envelope, round-trip, edit locality, assembler, validator)

## Broken / incomplete

| Issue | Impact |
|---|---|
| UnO alternate-CC / stompbox / tri-state **byte map unknown** | UI correctly locks these; not yet editable |
| Chunked **write** path was documented but not fully wired into device service | Added in this iteration |
| Live TEST mode did not send real MIDI | Fixed: channel-voice preview path |
| Diagnostics hex view only first 96 transport bytes | Expanded to decoded memory inspector |
| Demo config on cold start (fake presets) | Misleading until first Read/Open |
| FieldMap transport offsets incomplete for decoded view | Improved |
| Stock firmware auto-upload | Still requires manual SYSEX SEND |
| No ControlCenter session comparator yet | Protocol lab diff tooling added |
| Fixture metadata for UnO incomplete in tree | Added |

## Protocol assumptions present (and their standing)

| Assumption | Standing |
|---|---|
| Official 16-byte preset layout | CONFIRMED (Behringer PDF) |
| Enable = bit7 clear on command bytes | CONFIRMED |
| Calibration UI transform ±8/±5 | OBSERVED (OSS lineage); raw bytes preserved |
| `0x0E` in probe = UnO | HARDWARE heuristic; manual override provided |
| Reserved 416 bytes = UnO extensions | HYPOTHESIS only |

## Open-source / reference implementations

- https://github.com/riban-bw/fcb1010 (MIT, stock-focused Python)
- https://github.com/trafficpest/fcbtool (MIT, C port of riban + fixtures)
- Behringer FCB1010 SysEx Structure PDF
- FCB/UnO ControlCenter manual (behavioral reference only — no code/assets copied)
- Mountain Utilities FCB1010 Manager (stock 2.4/2.5; UnO unsupported)
- UnO 1.0.3/1.0.4 user guides + changelog

## Known-editor behavioral research

ControlCenter: same 2352 dump, optional 16×160 chunking on UnO ≥1.0.3, always-on receive, stompbox UI with alternate CC values, `.lgp` also stores names (Wino docs). Names are **not** in the MIDI dump — project-only metadata matches our `.fcbproject` design.
