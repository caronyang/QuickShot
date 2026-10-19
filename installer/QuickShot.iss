; QuickShot — Inno Setup script (per-user install, no admin rights needed)
; Build with:  iscc /DPublishDir=<publish dir> installer\QuickShot.iss
; or just run build\publish.ps1 which does both steps.

#define MyAppName "QuickShot"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "QuickShot"
#define MyAppExeName "QuickShot.exe"

#ifndef PublishDir
  #define PublishDir "..\build\publish"
#endif

[Setup]
AppId={{3F1B9C42-5D7A-4E60-9B21-7C4A8E6D2F35}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}

; Per-user install: %LocalAppData%\Programs\QuickShot, never asks for elevation.
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UsePreviousAppDir=yes

OutputDir=..\build\installer
OutputBaseFilename=QuickShot-Setup-{#MyAppVersion}
SetupIconFile=..\src\QuickShot\Assets\quickshot.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; Inno Setup shipped the unofficial translations up to 6.4; newer versions do not, so this
; is conditional. Pass /DUseChineseTrad when ChineseTrad.isl is available on the build
; machine (build\publish.ps1 does that automatically).
#ifdef UseChineseTrad
Name: "chinesetraditional"; MessagesFile: "compiler:Languages\ChineseTrad.isl"
#endif

[Tasks]
Name: "autostart"; Description: "Start QuickShot when Windows starts (recommended)"; GroupDescription: "Startup and shortcuts:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Startup and shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Same per-user Run value that QuickShot itself reads and writes (settings page).
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "QuickShot"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Start QuickShot now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/IM {#MyAppExeName} /F"; Flags: runhidden skipifdoesntexist

[Code]
// A tray app has no restart-manager visible window, so end a running instance explicitly
// before the files are replaced.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/IM {#MyAppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
