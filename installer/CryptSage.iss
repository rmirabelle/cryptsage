; Crypt Sage installer — Inno Setup 6 script
; Build: "C:\Users\rmira\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer\CryptSage.iss
; Output: installer\Output\CryptSage-Setup.exe
;
; Crypt Sage ships VeraCrypt's CLI binaries + kernel driver in the app folder and uses
; VeraCrypt's portable mode — the driver is loaded on-demand when mounting and unloaded
; when the app closes. No permanent VeraCrypt installation is required on the target system.
; VeraCrypt is Apache 2.0 licensed; its LICENSE and NOTICE are included in the install dir.

#define MyAppName         "Crypt Sage"
; publish.ps1 passes the version of CryptSage.App.csproj with /DMyAppVersion=...
#ifndef MyAppVersion
  #define MyAppVersion    "0.1.0"
#endif
#define MyAppPublisher    "Crypt Sage"
#define MyAppExeName      "CryptSage.App.exe"
#define MyAppId           "{{B3C9A5F2-8D4E-4B6A-9C7F-5E1D2A8B3C4F}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\CryptSage
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=CryptSage-Setup
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\CryptSage.App\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\{#MyAppExeName}";      DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\CryptSage.App\AppIcon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "vendor\VeraCrypt\*";               DestDir: "{app}\VeraCrypt"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\AppIcon.ico"

[Run]
; shellexec routes through ShellExecute so the UAC elevation declared in the app
; manifest is honored. Without it, Inno Setup's default de-elevation of postinstall
; actions fails with "The requested operation requires elevation".
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent shellexec
; In-app update: the app runs this installer with /SILENT and closes. This entry
; starts the app again when the silent install finishes.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait skipifnotsilent shellexec
