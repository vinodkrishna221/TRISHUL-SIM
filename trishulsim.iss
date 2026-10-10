; TRISHUL-SIM · InnoSetup 6 Installer Script (US-P2-B)
; Standard: Ministry of Defence (MoD) Problem SIH26247 · Air-Gapped Standalone Simulation Trainer
; Architecture: Windows x86-64, IL2CPP Native Master Build

#define MyAppName "TRISHUL-SIM C-UAS Trainer"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "TRISHUL"
#define MyAppExeName "TRISHUL-SIM.exe"
#define OutputDate "20261008"

[Setup]
AppId={{D37B4A58-E810-4A92-BF14-2C9794E78120}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\TRISHUL-SIM
DefaultGroupName=TRISHUL-SIM
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=TRISHUL-SIM-installer-{#OutputDate}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "Build\TRISHUL-SIM\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
