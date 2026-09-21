# Chunked SysEx (UnO ≥ 1.0.3)

## Verified

| Item | Status |
|---|---|
| Request `F0 00 20 32 01 0C 50 F7` | HARDWARE — 160-byte reply, command byte `10` |
| Requests `51..5F` | HARDWARE — replies `11..1F`, length 160 |
| Full dump still preferred | HARDWARE — `4F` reliable on CME WIDI |

## Not verified (do not enable in production UI)

Reassembling sixteen 160-byte replies into a byte-identical 2352-byte dump failed under differential analysis on WIDI:

- Chunk 0 payload matched `full[7..158]` (152 bytes).
- Later chunks were **not** the sequential continuation of the dump; payloads were near-duplicates with first-byte ≈ `index*8`.
- Possible causes: BLE fragmentation artifacts, ControlCenter-specific framing not yet matched, or chunk protocol requiring a different negotiation than bare `50..5F` polls.

Until a ControlCenter session capture proves the exact split/merge, the app:

- Uses **full** 2352-byte read/write.
- Exposes `CaptureChunksForLabAsync` for research only.
- Refuses PreferWriteMode/ReadMode=Chunked with an explicit error.
