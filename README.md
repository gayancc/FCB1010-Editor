# FCB1010 Studio

FCB1010 Studio is a free, open-source Windows editor and SysEx librarian for the Behringer
FCB1010. It provides a compact pedalboard UI, all 100 presets, global MIDI routing, raw dump
capture, byte comparison, automatic backups, validation, and real MIDI input/output through the
Windows MIDI APIs.

## Firmware support

| Firmware | Status |
|---|---|
| Stock v2.5-family | Known fields implemented and fixture-tested |
| Regular UnO 1.0.3/1.0.4 family | Firmware probe, automatic read, full/chunk protocol, write and read-back verification hardware-tested |
| UnO2 | Not supported; it is a different architecture |

Common preset and global fields use the decoded 2,048-byte memory map. Unknown regular-UnO bytes are
preserved exactly so opening and editing a dump cannot silently erase firmware-specific state.

## Build and run

Requirements: Windows 11 and the .NET 10 SDK.

```powershell
dotnet restore FCB1010.slnx
dotnet test FCB1010.slnx -c Release
dotnet run --project src/FCB1010.App/FCB1010.App.csproj
```

The desktop shell is **Avalonia 12** (native Windows executable, MVVM, custom-drawn digital-twin pedalboard). Core protocol and MIDI transport remain .NET class libraries.

Create the self-contained Windows executable:

```powershell
powershell -ExecutionPolicy Bypass -File build/package.ps1
```

The executable is written to `artifacts/win-x64/FCB1010Studio.exe`.

## MIDI setup

1. Connect the FCB1010 MIDI OUT to the interface MIDI IN, and interface MIDI OUT to FCB1010 MIDI IN.
2. Select MIDI IN and MIDI OUT independently, then select **Connect**.
3. Choose **Read From FCB1010**. Regular UnO is requested automatically. For stock firmware, enter
   Global Configuration and trigger **SYSEX SEND** with switch 6.
4. Review and edit the dump. Save a `.fcbproject` to retain names/notes or `.syx` for a raw dump.
5. Before writing, enable SysEx receive on firmware versions that require it. Select **Write To
   FCB1010** and confirm the destructive action. The app backs up the last device-read dump first.
6. The app requests the stored dump and reports success only when all 2,352 bytes match.

For CME WIDI / BLE MIDI, pair and expose the WIDI endpoint to Windows first. If it appears in the
Windows MIDI port list, it appears in the app; no product-name matching is used. Large 2,352-byte
dumps may exceed the buffering of some BLE/USB adapters. The verified regular-UnO chunk requests
are `0x50` through `0x5F`, one 160-byte response per chunk.

## Safety and known limitations

- Reading regular UnO sends only the verified upload request `F0 00 20 32 01 0C 4F F7`.
- A write is reported successful only after automatic, byte-identical device read-back.
- Regular UnO stompbox/alternate-value bytes are preserved but not yet exposed for editing (byte map UNVERIFIED — see `docs/protocol/uno-unknowns.md`).
- Chunk commands `50..5F` are hardware-observed; full dump reassembly from chunks is **not** production-enabled yet (`docs/protocol/chunked-sysex.md`).
- Hardware verification used a regular-UnO FCB1010 over CME WIDI; controlled PC1 round-trips restored the original setup.

See [audit](docs/AUDIT.md), [evidence matrix](docs/protocol/evidence-matrix.md), [architecture](docs/architecture.md), [stock protocol](docs/fcb1010-protocol.md),
[regular UnO protocol](docs/uno-protocol.md), and [hardware testing](docs/hardware-testing.md).

## License

MIT. See [LICENSE](LICENSE) and [license attribution](docs/license-attribution.md).
