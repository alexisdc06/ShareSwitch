; ShareSwitch — Inno Setup 6 script
; Build: ISCC.exe /DAppVersion=x.y.z installer\ShareSwitch.iss
; (expects the published exe in ..\publish\ShareSwitch.exe — run publish.ps1 first)

#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif

#define AppName "ShareSwitch"
#define Publisher "AC Design Studio"
#define AppExe "ShareSwitch.exe"

[Setup]
AppId={{8F4E2C71-5A9B-4D3E-B7C2-A1E6F0D89B34}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/alexisdc06/ShareSwitch
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
; Per-user install: no UAC prompt, friendlier for an unsigned indie app.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Gracefully close a running instance before install/uninstall
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "autostart"; Description: "Start {#AppName} when Windows starts"; Flags: unchecked

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent
