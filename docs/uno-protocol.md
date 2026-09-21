# Regular UnO protocol status

This document concerns regular UnO, not UnO2. Findings combine public manuals/source with controlled
tests on a physical regular-UnO FCB1010 connected through CME WIDI.

## Hardware-verified commands

| Purpose | Request | Response |
|---|---|---|
| Firmware identity | `F0 00 20 32 01 0C 40 F7` | 9 bytes; tested unit returned signature `09 0E` |
| Current patch | `F0 00 20 32 01 0C 45 F7` | `F0 00 20 32 01 0C 00 pp F7` |
| Full upload | `F0 00 20 32 01 0C 4F F7` | complete 2,352-byte dump |
| Chunk upload | command `50` through `5F` | one 160-byte message, command `10` through `1F` |

The tested full write is the normal 2,352-byte dump itself. After writing, the app sends the full
upload request and requires a byte-identical response. A controlled PC1 edit and subsequent restore
both passed this check.

## Confirmed behavior

| Feature | Status |
|---|---|
| Stock-compatible 100 × 16-byte preset memory | decoded, editable, hardware-tested |
| Full patchdump | hardware-tested |
| Optional sixteen 160-byte chunks | framing and commands hardware-tested |
| Firmware and current-patch queries | hardware-tested |
| SysEx receive without manual setup on recent UnO | hardware-tested |
| Five global stomps + 19 banks of five presets | behavior documented; extra offsets not mapped |
| Main/alternate CC values and momentary stomps | behavior documented; extra offsets not mapped |
| Per-preset stored stomp states and tri-state actions | behavior documented; extra offsets not mapped |

Unknown extended fields are preserved from the source dump. They are not reset, normalized, or
presented as editable controls. This gives regular-UnO users safe automatic read/write for common
fields without pretending the remaining stompbox layout is known.

## Hardware evidence

Baseline dump: `hardware-captures/FCB1010-UnO-hardware-20260921-132357.syx`

SHA-256: `E180D1DA289A949788B76391271192187121282233CC2F0EC391F4505EA228E1`

Sources: [ControlCenter manual](https://www.fcb1010.uno/downloads/FCB_UnO_ControlCenter_manual.pdf),
[UnO 1.0.4 guide](https://www.fcb1010.eu/downloads/FCB_UnO_v1_0_4_UserGuide.pdf), and
[official FCB1010 memory format](https://www.parts-express.com/pedocs/more-info/248-6208-behringer-fcb1010--40933.pdf).
