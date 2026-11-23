; =====================================================================
; Script de Instalacion Inno Setup para Limpiador de RAM
; =====================================================================

#define MyAppName "Limpiador de RAM"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "LimpiadorRAM"
#define MyAppExeName "LimpiadorRAM.exe"

[Setup]
AppId={{D37E8831-29C4-4B22-86FD-A8F142D3199B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userappdata}\Programs\LimpiadorRAM
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=Setup_LimpiadorRAM_Inno
SetupIconFile=resources\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startupicon"; Description: "Iniciar con Windows en segundo plano"; GroupDescription: "Opciones de inicio:"

[Files]
Source: "LimpiadorRAM.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "resources\app.ico"; DestDir: "{app}\resources"; Flags: ignoreversion
Source: "resources\panda_clean.png"; DestDir: "{app}\resources"; Flags: ignoreversion
Source: "resources\watermark_bg.jpg"; DestDir: "{app}\resources"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\resources\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\resources\app.ico"; Tasks: desktopicon
Name: "{autostartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "/tray"; IconFilename: "{app}\resources\app.ico"; Tasks: startupicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LimpiadorRAM"; ValueData: """{app}\{#MyAppExeName}"" /tray"; Tasks: startupicon; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
