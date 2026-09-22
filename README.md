# FCB1010 Studio

Free, open-source Windows editor and SysEx librarian for the Behringer FCB1010.
Built with **.NET 10** and **Avalonia 12**. Supports stock and regular UnO firmware (UnO2 is out of scope).

## Download (Windows)

Self-contained executable — no .NET install required:

**https://github.com/gayancc/FCB1010-Editor/releases/latest**

| Asset | Use |
|---|---|
| `FCB1010Studio-*-win-x64.zip` | Recommended |
| `FCB1010Studio-*-win-x64.exe` | Single-file download |

If SmartScreen blocks the first run: right-click → **Properties** → **Unblock**.

## Build

Requires Windows 11 and the .NET 10 SDK.

```powershell
dotnet restore FCB1010.slnx
dotnet test FCB1010.slnx -c Release
dotnet run --project src/FCB1010.App/FCB1010.App.csproj
```

Package a release zip/exe:

```powershell
powershell -ExecutionPolicy Bypass -File build/package.ps1
```

### GitHub Release (auto-bump)

1. Push your branch.
2. **Actions** → **Release** → **Run workflow**
3. Choose **patch** / **minor** / **major**
4. CI bumps the version, tags `vX.Y.Z`, and attaches the Windows build.

## MIDI setup

1. Wire FCB1010 MIDI OUT → interface IN, and interface OUT → FCB1010 IN.
2. Select IN/OUT ports, then **LINK**.
3. **READ** from the device (UnO is requested automatically; stock needs Global Config → SYSEX SEND on switch 6).
4. Edit, then **WRITE**. The app backs up first and verifies with a byte-identical read-back.

### Preset and bank names

The FCB1010 SysEx dump has no text-name fields. Preset and bank names are therefore editor metadata, not settings stored inside the pedal. **WRITE** keeps them in a local record tied to the exact verified dump, so a later **READ** of that same dump restores them on this computer. If only names changed, the app saves that record without sending an unnecessary SysEx write. Save a `.fcbproject` for a portable copy; a plain `.syx` file contains MIDI settings but no names.

## License

MIT. See [LICENSE](LICENSE) and [ThirdPartyNotices.txt](ThirdPartyNotices.txt).
