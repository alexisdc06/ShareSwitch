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
; Brand panels (glass SS + lockup); 100% and 250% so they stay sharp on high-DPI screens
WizardImageFile=..\assets\installer\wizard-large-100.bmp,..\assets\installer\wizard-large-250.bmp
WizardSmallImageFile=..\assets\installer\wizard-small-100.bmp,..\assets\installer\wizard-small-250.bmp
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
; The exe embeds Figtree (SIL OFL 1.1); the licence travels with it
Source: "..\assets\fonts\OFL.txt"; DestDir: "{app}"; DestName: "Figtree-OFL.txt"; Flags: ignoreversion

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

[Code]
// Brand header for the inner pages: a taller navy band, white page title,
// lavender description and a larger app icon, all vertically centred with
// comfortable padding — instead of Inno's default text hugging the top edge.
const
  BrandNavy = $002F1517;      // #17152F as BGR
  BrandLavender = $00FFBCC2;  // #C2BCFF as BGR

procedure InitializeWizard;
var
  PanelH, Delta, Pad, IconSize, TextW, NameH, DescH, Gap, TextTop, ContentGap: Integer;
begin
  PanelH := ScaleY(96);
  Pad := ScaleX(28);
  IconSize := ScaleY(60);
  ContentGap := ScaleY(16);
  Delta := PanelH - WizardForm.MainPanel.Height;

  // Grow the header and push the page content down by the same amount, plus
  // a little breathing room under the divider.
  WizardForm.MainPanel.Height := PanelH;
  WizardForm.MainPanel.Color := BrandNavy;
  WizardForm.Bevel1.Top := WizardForm.Bevel1.Top + Delta;
  WizardForm.InnerNotebook.Top := WizardForm.InnerNotebook.Top + Delta + ContentGap;
  WizardForm.InnerNotebook.Height := WizardForm.InnerNotebook.Height - Delta - ContentGap;

  // Larger icon, centred vertically on the right.
  WizardForm.WizardSmallBitmapImage.Stretch := True;
  WizardForm.WizardSmallBitmapImage.BackColor := BrandNavy;
  WizardForm.WizardSmallBitmapImage.SetBounds(
    WizardForm.MainPanel.ClientWidth - Pad - IconSize, (PanelH - IconSize) div 2, IconSize, IconSize);

  // Title + description as one block, centred vertically.
  TextW := WizardForm.MainPanel.ClientWidth - Pad * 3 - IconSize;
  NameH := ScaleY(26);
  DescH := ScaleY(34);
  Gap := ScaleY(2);
  TextTop := (PanelH - NameH - Gap - DescH) div 2;

  WizardForm.PageNameLabel.Font.Color := clWhite;
  WizardForm.PageNameLabel.Font.Size := 11;
  WizardForm.PageNameLabel.SetBounds(Pad, TextTop, TextW, NameH);

  WizardForm.PageDescriptionLabel.Font.Color := BrandLavender;
  WizardForm.PageDescriptionLabel.SetBounds(Pad, TextTop + NameH + Gap, TextW, DescH);
end;
