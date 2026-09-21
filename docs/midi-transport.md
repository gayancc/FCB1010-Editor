# MIDI transport

The transport enumerates MIDI IN and OUT independently through DryWetMIDI's Windows backend. Port
IDs come from the operating system; no device name is hardcoded, so USB DIN and WIDI/BLE endpoints
work when Windows exposes them as MIDI ports.

`SysExAssembler` accepts fragments, ignores bytes before F0, rejects illegal status bytes inside a
message, completes only on F7, resets on a new F0, and reports the exact partial byte count on timeout.
The device service accepts only a complete, structurally valid 2,352-byte FCB1010 dump.

Diagnostics records timestamp, direction, event type, size, status, and optionally raw hex. Driver
exceptions move the connection state to Error. Disposal closes ports so another program can use them.

Regular UnO reads send the hardware-verified full upload request `F0 00 20 32 01 0C 4F F7`.
Chunk requests use command bytes `50` through `5F` and return 160-byte messages whose command bytes
are `10` through `1F`. After a write, the device service requests a full upload and compares all 2,352
bytes; a driver-complete send without an identical read-back is reported as unverified/failure.
