# WinNUT 2.0

A modern **NUT (Network UPS Tools) client** for **Windows and Linux** with a retro, KITT-style dashboard:
glowing LED gauges, slanted 7-segment readouts and a Knight-Rider scanner bar. It monitors a UPS that is
shared by a NUT server (for example a **TerraMaster NAS**, Synology, a Raspberry Pi, ...) and can shut down
your PC cleanly when the power fails.

> **Derived from WinNUT-Client.** WinNUT 2.0 is based on the idea of the original
> [WinNUT-Client by gawindx](https://github.com/gawindx/WinNUT-Client). It is a from-scratch rewrite in
> C# / .NET 9 / Avalonia; no source code of the original was copied. Many thanks to gawindx for the original.
>
> **Free to use, as is. It may never be sold or offered for money.** See [LICENSE](LICENSE).

## Features
- Live gauges: battery %, load %, runtime, input voltage, plus output voltage / frequency when the UPS reports them.
  UPSes that do not report those (e.g. APC Back-UPS over USB) automatically show estimated **power (W)** and **battery voltage** instead.
- Status line, event log, tray icon with status colour, minimise to tray.
- Automatic shutdown with a configurable delay, by: minutes on battery, battery %, estimated runtime, low-battery flag (LB), forced shutdown (FSD) from the server. "Cancel pending shutdown" is in the tray menu.
- **Test mode** (Settings): evaluates the shutdown rules and only logs what would happen, so you can try thresholds safely.
- Mains voltage gauges are centred on the UPS nominal voltage (green around 230 V, red towards the transfer limits).
- NUT password is stored encrypted (Windows DPAPI, current user only).
- Automatic reconnect when the server or network goes away.
- Windows installer (no .NET install needed), optional start with Windows.

## Install (Windows)
Download `WinNUT2-Setup-x.y.z.exe` from the [Releases](https://github.com/demofilm/terra_nut/releases) page and run it.
It installs per user without admin rights (choose "for all users" in the dialog if you want).
### "Windows protected your PC" (SmartScreen)
The installer is **not code-signed** (a certificate costs money and this project is free), so Windows SmartScreen shows a warning for downloaded files it does not know yet. This is normal for small free tools. You can continue in one of these ways:

1. In the blue SmartScreen window click **More info**, then **Run anyway**.
2. Or, before running: right-click the downloaded `.exe` → **Properties** → tick **Unblock** at the bottom → OK. In PowerShell: `Unblock-File .\WinNUT2-Setup-2.0.1.exe`.

Check that you have the real file: the SHA-256 checksum is in the release notes. Compare with `Get-FileHash .\WinNUT2-Setup-2.0.1.exe -Algorithm SHA256`.

If you use **Smart App Control** (Windows 11, "On" mode), Windows may block unsigned apps completely, without a "Run anyway" button. Then either build the app yourself (see *Build from source*), or switch Smart App Control to *Evaluation/Off* in *Windows Security → App & browser control* (note: once turned off it cannot be turned on again without reinstalling Windows). The app does not need admin rights and does not change system files.

Then open **Settings** and enter the NAS address, UPS name (usually `ups`) and, if required, the NUT user and password.
Settings are stored in `%APPDATA%\WinNUT2\settings.json` (Linux: `~/.config/WinNUT2/settings.json`).

## TerraMaster / NAS setup
Enable the UPS / NUT server in the NAS (TOS: UPS settings → allow network UPS clients), make sure this PC is allowed to
connect, and use port **3493**. You can check what your NAS offers with any NUT client, or:
```
LIST UPS
LIST VAR ups
```
sent to port 3493. Tested with a TerraMaster NAS and an APC Back-UPS BGM2200B-GR.

## Build from source
Requires the .NET 9 SDK.
```
dotnet build
dotnet test
dotnet run --project tools/WinNUT.FakeServer      # fake NUT server: type ol | ob | lb | fsd
dotnet run --project src/WinNUT.App
installer\build-installer.ps1                      # Windows installer (needs Inno Setup 6)
```
| Folder | Purpose |
|---|---|
| `src/WinNUT.Core` | NUT protocol client, status parsing, monitor, shutdown policy, settings. No UI. |
| `src/WinNUT.App` | Avalonia UI: gauges, tray, settings. |
| `tests/` | 38 xUnit tests (incl. a fake NUT server over real TCP). |
| `tools/WinNUT.FakeServer` | Fake NUT server for manual testing. |
| `installer/` | Inno Setup script and build script. |

## Status and limits
- **Linux:** the unit tests pass and the app builds and runs on Linux (.NET 9 SDK container, X11 via Xvfb, against the fake server, including test mode). It has not been run on a real Linux desktop or against real Linux shutdown rights yet. Shutdown there needs root or a sudoers rule; tray icons depend on the desktop environment.
- **Password:** encrypted with DPAPI on Windows. On Linux it is stored in plain text in `settings.json` (file mode 600).
- **Shutdown:** the shutdown command line and cancel were verified for real on Windows (a shutdown 1 hour out was scheduled and cancelled again). The full policy was tested with a fake server and test mode. **Test your thresholds before relying on them.**
- Not (yet) included from the original: `ups.ini` import, auto-update, translations. Notifications are expected to come from the NAS itself.
- If `settings.json` is not valid JSON, the app falls back to defaults.
## License
Free use, no selling, no warranty: see [LICENSE](LICENSE). Third-party components: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).


