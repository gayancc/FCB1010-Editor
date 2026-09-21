# Research findings

## Reference repositories

`riban-bw/fcb1010` is a compact Python implementation tested by its author with stock v2.5
firmware. It validates a 2,352-byte envelope, maps 100 presets and global channels, and documents
that its global write coverage is incomplete. Its field order and offsets agree with the public
format sheet. It explicitly says it was not tested with UnO.

`trafficpest/fcbtool` is a C/ncurses editor with SysEx capture/transmit, CSV conversion and timestamped
backups. Its codec is described in-source as a conversion of Brian Walton's Python work with a few
fixture-driven adjustments. It includes several 2,352-byte stock dumps. The project is useful as a
second implementation and fixture source, not independent proof of regular-UnO mapping.

## License assessment

Both repositories use the MIT License and permit study, modification and redistribution with their
copyright/license notice retained. This project uses an independent implementation and records both
authors in `ThirdPartyNotices.txt`. Only the upstream V-AMP dump is included, encoded as a test fixture
with provenance. The public ControlCenter manual was read as documentation; its program, resources
and `.lgp` format were not copied or reverse engineered. Public fcb1010.online code corroborated the
official 7-to-8-bit packing and decoded stock memory fields.

## Stock protocol summary

- complete dump is 2,352 bytes with `F0 00 20 32 01 0C 0F` header and `F7` terminator;
- 100 presets occupy 1,600 decoded bytes, 16 bytes per preset;
- five PCs, two CC number/value pairs, note, two expression controller/min/max triplets and two relay
  bits are mapped;
- function MIDI channels and selected global flags/calibration values occur near the dump tail;
- a no-edit parse/serialize is byte-exact because unmodelled data is preserved.

## Known regular-UnO differences

Public UnO manuals confirm the same 2,352-byte full patchdump size and document stompbox mode,
alternate CC values, per-preset stomp state, toggle/momentary behavior, tri-state relay/expression
actions, and the optional v1.0.3 16×160-byte transfer. The sources do not publish the memory offsets
for extended fields. UnO 1.0.3 restored PC4 and removed older short programmable-SysEx/transpose
storage features. Hardware probing established command `40` for identity, `45` for current patch,
`4F` for a full upload, and `50..5F` for the sixteen chunks.

## Unknowns that require hardware

Stompbox/alternate-value offsets and relay/expression tri-state encoding remain unverified. The
editor preserves those source bytes. Firmware identification, full and chunk uploads, full write,
and byte-exact read-back are verified on physical regular-UnO hardware.

## Implementation phases

1. Evidence and licensing - complete for available public sources.
2. Protocol documentation - stock mapping documented; UnO unknowns explicit.
3. Core parser/model/serializer - complete for mapped common fields with lossless preservation.
4. Automated tests - stock fixture, malformed input, indexing, values, assembly and comparison.
5. MIDI transport/diagnostics - implemented through Windows MIDI with real ports and SysEx.
6. Hardware verification - complete for WIDI transport, common fields, full write/read and restore.
7. Desktop UI - implemented for common editing, global settings and diagnostics.
8. Device wiring - automatic probe/read, write, timeout, backup and byte-exact verification implemented.
9. UnO-specific editing - blocked on verified byte mappings; no guessed writes.
10. Packaging - self-contained Windows x64 executable produced.
