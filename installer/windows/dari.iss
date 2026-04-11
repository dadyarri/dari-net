; Inno Setup script for Dari
; Build:  ISCC.exe /DMyAppVersion=1.0.0 /DMyPublishDir=C:\path\to\publish  dari.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#ifndef MyPublishDir
  #error MyPublishDir must be defined (absolute path to the published output directory)
#endif

#define MyAppName       "Dari"
#define MyAppExeName    "Dari.App.exe"
#define MyAppPublisher  "dadyarri"
#define MyAppURL        "https://github.com/dadyarri/dari-net"
#define MyAppAssocExt   ".dar"
#define MyAppAssocKey   "Dari.DarArchive"
#define MyAppId         "{F4A8E9C1-3B2D-4E6F-A1C7-9D8B5E0F2A3C}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Install per-user into %LOCALAPPDATA%\Dari\app — no admin rights required
DefaultDirName={localappdata}\{#MyAppName}\app
PrivilegesRequired=lowest

; Hide the dir/group selection pages — location is fixed
DisableDirPage=yes
DisableProgramGroupPage=yes

; Output
OutputDir={#SourcePath}\..\..\
OutputBaseFilename=dari-win-x64-installer

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; UI
WizardStyle=modern

; Icons
SetupIconFile={#SourcePath}\..\..\Dari.App\Assets\dari.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Tell Windows to refresh file-type associations after install/uninstall
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Registry]
; ── .dar extension ──────────────────────────────────────────────────────────
Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocExt}"; \
  ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; \
  Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocExt}"; \
  ValueType: string; ValueName: "Content Type"; \
  ValueData: "application/x-dari-archive"; \
  Flags: uninsdeletevalue

; ── ProgID ──────────────────────────────────────────────────────────────────
Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}"; \
  ValueType: string; ValueName: ""; ValueData: "Dari Archive"; \
  Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; \
  ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"

Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; \
  ValueType: string; ValueName: ""; \
  ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; ── Explorer context menu on .dar files ─────────────────────────────────────
Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\dari_open"; \
  ValueType: string; ValueName: ""; ValueData: "Open with Dari"; \
  Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\dari_open\command"; \
  ValueType: string; ValueName: ""; \
  ValueData: """{app}\{#MyAppExeName}"" ""%1"""

Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\dari_extract"; \
  ValueType: string; ValueName: ""; ValueData: "Extract here (Dari)"; \
  Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\dari_extract\command"; \
  ValueType: string; ValueName: ""; \
  ValueData: """{app}\{#MyAppExeName}"" --extract-here ""%1"""

; ── Uninstall helper key ─────────────────────────────────────────────────────
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; \
  ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; \
  Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; \
  Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent
