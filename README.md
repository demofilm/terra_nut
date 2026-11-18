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
- Automatic reconnect when the server or network goes away.
- Windows installer (no .NET install needed), optional start with Windows.

## Install (Windows)
Download `WinNUT2-Setup-x.y.z.exe` from the [Releases](https://github.com/demofilm/terra_nut/releases) page and run it.
It installs per user without admin rights (choose "for all users" in the dialog if you want).
The installer is not code-signed, so Windows SmartScreen may warn: choose *More info → Run anyway*.

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
| `tests/` | 21 xUnit tests (incl. a fake NUT server over real TCP). |
| `tools/WinNUT.FakeServer` | Fake NUT server for manual testing. |
| `installer/` | Inno Setup script and build script. |

## Status and limits
- Linux build is expected to work (Avalonia, `shutdown -h`) but **has not been tested yet**. Shutdown on Linux needs root or a sudoers rule. Tray icons depend on the desktop environment.
- The NUT password is stored in plain text in `settings.json`.
- Not (yet) included from the original: `ups.ini` import, auto-update, translations, toast notifications.
- Automatic shutdown has only been tested against a fake server. **Test your thresholds before relying on them.**

## License
Free use, no selling, no warranty: see [LICENSE](LICENSE). Third-party components: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
