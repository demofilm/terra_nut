# Builds the Windows installer: publish (self-contained win-x64) + Inno Setup.
param([string]$Version = "2.0.0")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install with: winget install JRSoftware.InnoSetup" }
if (Test-Path artifacts\publish) { Remove-Item artifacts\publish -Recurse -Force }
dotnet publish src/WinNUT.App -c Release -r win-x64 --self-contained true -o artifacts/publish -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version
if ($LASTEXITCODE) { throw "publish failed" }
& $iscc "/DAppVersion=$Version" installer\WinNUT2.iss
if ($LASTEXITCODE) { throw "ISCC failed" }
Get-Item "artifacts\WinNUT2-Setup-$Version.exe"
