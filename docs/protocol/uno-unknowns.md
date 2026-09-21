# Regular UnO unknowns requiring experiment

Regular UnO (not UnO2). Behavioral features are documented in the UnO 1.0.3/1.0.4 user guides and ControlCenter manual. **Byte offsets for extended fields are not published.**

## Suspected storage

Decoded addresses `0x640..0x7DF` (1600–2015) are unused (`0xFF`) on stock dumps and on UnO dumps that have never enabled stompbox / alternate-CC features. UnO's first-boot “extra memory initialization” strongly implies this region (or a subset) holds UnO extensions.

Hypothesis (UNVERIFIED — do not write blindly):

| Bytes | Candidate content |
|---|---|
| 100 × 2 | CC1/CC2 alternate values per preset |
| 100 × 1–2 | Toggle/momentary flags + stomp target states |
| ~16 global | Stompbox mode, row select, shared stomp definitions |

Until differential experiments mark fields VERIFIED, the editor **preserves** these bytes from `SourceSysEx` and does not expose them as editable controls that rewrite unknowns.

## Required differential experiments

1. Backup current dump.
2. Enable stompbox mode on the device (Global Config) → dump B → diff vs A.
3. Set CC1 alternate value on preset 00 → dump C → diff.
4. Enable CC1 toggle → dump D → diff.
5. Set per-preset stomp targets (on/off/unchanged) → dump E → diff.
6. Record each changed decoded offset in `docs/protocol/evidence-matrix.md` and add a fixture under `tests/fixtures/uno/`.

Use `tools/FCB1010.HardwareProbe` commands: `request-dump`, `diff`, `decode`, `roundtrip-test`.

## Safe editing today

Common stock-compatible fields (PC/CC/Note/Exp/relays/channels/Direct Select/Running Status/MIDI Merge) are hardware-verified for read/edit/write/read-back on regular UnO over WIDI. Unknown UnO bytes survive because serialization starts from the decoded source dump.
