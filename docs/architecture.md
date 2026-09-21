# Architecture and technology decision

## Stack evaluation

| Option | Strengths | Decision |
|---|---|---|
| Avalonia 12 on .NET 10 | Native desktop UI, custom drawing/Skia, DPI-aware, MVVM, Windows executable packaging | **Chosen for the Windows 11 shell** |
| WPF on .NET 10 | Mature Windows desktop stack | Superseded by Avalonia 12 for this product UI |
| Electron / Tauri | Strong web UI ecosystem | Not chosen: browser/runtime boundary hurts native MIDI and SysEx troubleshooting |
| WinUI 3 | Modern Microsoft UI | Not chosen: packaging complexity with no protocol advantage |

DryWetMIDI provides enumerated Windows MIDI devices and native SysEx events. It is isolated behind
`IMidiTransport`, so Core and future transports remain portable.

## Layers

1. **Protocol/Core** (`FCB1010.Core`) - strict envelope parsing, known field mapping, serializer,
   validation, raw-byte preservation, field-aware dump comparison.
2. **MIDI Transport** (`FCB1010.Midi`) - input/output discovery, independent port selection,
   connection lifecycle, fragmented SysEx assembly, driver error reporting, diagnostics.
3. **Device Communication** - firmware detection, automatic regular-UnO upload requests, receive
   timeout, full-dump validation, serialized writes, last-read backup, and byte-exact verification.
4. **Configuration Model** - strongly typed 100-preset domain model; UI controls are not the model.
5. **Persistence** - `.syx` and versioned, readable `.fcbproject` JSON with embedded original dump.
6. **UI** (`FCB1010.App`) - Avalonia 12 MVVM desktop shell: command deck, custom-drawn digital-twin
   pedalboard controls, contextual inspector, bank map, signal routing, diagnostics/hex inspector.
7. **Diagnostics** - raw hexadecimal view, timestamped traffic and byte-by-byte comparisons.

The serializer starts from the original raw dump when present and changes only mapped bits/bytes.
This preserves firmware-dependent and unknown bytes and enables byte-exact no-edit round trips.
