; WinNUT 2.0 installer (Inno Setup 6). Build with: installer\build-installer.ps1
#define AppName "WinNUT 2.0"
#ifndef AppVersion
  #define AppVersion "2.0.1"
#endif
#define AppExe "WinNUT2.exe"

[Setup]
AppId={{B7C1A6E4-3D0F-4B6B-9C54-7E2A1F5D8A30}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=demofilm
AppPublisherURL=https://github.com/demofilm/terra_nut
AppSupportURL=https://github.com/demofilm/terra_nut/issues
DefaultDirName={autopf}\WinNUT 2.0
DefaultGroupName=WinNUT 2.0
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\WinNUT.App\Assets\icon.ico
OutputDir=..\artifacts
OutputBaseFilename=WinNUT2-Setup-{#AppVersion}
LicenseFile=..\LICENSE
Compression=lzma2/ultra
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start WinNUT 2.0 when I sign in to Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Excludes: "*.pdb,createdump.exe"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\WinNUT 2.0"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall WinNUT 2.0"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WinNUT 2.0"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WinNUT2"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Settings live in %APPDATA%\WinNUT2 and are intentionally kept on uninstall.

