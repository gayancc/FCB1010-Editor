# Real hardware testing

## Safe baseline

1. Connect both MIDI directions and select ports independently.
2. Select Read; on stock firmware manually trigger Global Configuration → SYSEX SEND.
3. Save the untouched `.syx`, record device serial/firmware/date/interface, and make a second copy.
4. Open and immediately save as SysEx. Confirm Compare reports zero differences.
5. Do not write until a known-good raw backup exists outside the application backup folder.

## Controlled protocol experiment

1. Capture baseline A.
2. Change exactly one hardware parameter.
3. Capture B.
4. Compare A/B and record offset, old/new byte, XOR mask, UI action, firmware and interface.
5. Repeat in the opposite direction and on a second preset to distinguish data from packing bits.
6. Add a fixture only when provenance is verified.

## Write verification

Enable SysEx receive if required by older firmware. The app writes, requests a fresh upload, and
compares all bytes automatically. A driver-complete result alone is never treated as storage proof.

## WIDI/BLE notes

Pair WIDI in Windows before launching or select Refresh Devices after pairing. Keep other MIDI apps
closed because Windows MIDI endpoints may be exclusive. If a single dump truncates, record the exact
received byte count. Verified UnO chunk requests are `F0 00 20 32 01 0C 50..5F F7`; each response
is 160 bytes and identifies its chunk with command byte `10..1F`.

## 2026-09-21 WIDI verification

- Ports: `FCB1010 IN` and `FCB1010 OUT` over CME WIDI.
- Firmware probe request `... 40 F7` returned signature `09 0E`, identifying regular UnO.
- Current-patch request `... 45 F7` returned patch `0x20`.
- Full upload request `... 4F F7` returned a valid 2,352-byte dump.
- The untouched dump was written and read back with identical SHA-256.
- Preset 00-1 PC1 was changed from 1 to 99; the resulting dump read back byte-identically.
- The original dump was restored and again read back byte-identically.

## 2026-09-21 WIDI verification (session 2 — continued)

- Ports: `FCB1010 IN` / `FCB1010 OUT` (CME WIDI).
- Probe `40` → `09 0E` (regular UnO) — reconfirmed.
- Current patch `45` → `00 20`.
- Full upload `4F` → validated 2352-byte dump saved.
- Controlled PC1 edit 0→99: write + read-back **byte-identical**; restore **byte-identical**.
- Chunk request `50` → 160-byte reply cmd `10` (framing HARDWARE). Reconstructing a full dump from `50..5F` replies is **not** byte-exact on this interface yet — production path stays full `4F` / full write.
- Protocol lab: `diff` / `decode` probe commands operational against live captures.

Baseline (earlier same day): SHA-256 `E180D1DA289A949788B76391271192187121282233CC2F0EC391F4505EA228E1`.

Round-trip capture: `hardware-captures/FCB1010-UnO-live-roundtrip-20260921-152100.syx`.

