[Setup]
AppId={{7855ECF0-2407-46A9-A1C0-8BA8DD55B010}
AppName=Heimora
AppVersion=0.7.2
AppPublisher=Heimora
DefaultDirName={autopf}\Heimora
UsePreviousAppDir=no
UsePreviousGroup=no
DefaultGroupName=Heimora
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=release
OutputBaseFilename=Heimora-Setup-0.7.2-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=assets\app.ico
UninstallDisplayIcon={app}\Heimora.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
Uninstallable=not IsTestInstall
[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
[Files]
Source: "dist-heimora\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Heimora"; Filename: "{app}\Heimora.exe"; Check: not IsTestInstall
Name: "{group}\Handleiding en beveiliging"; Filename: "{app}\Heimora.exe"; Parameters: "--help"; Check: not IsTestInstall
Name: "{autodesktop}\Heimora"; Filename: "{app}\Heimora.exe"; Check: not IsTestInstall
[Run]
Filename: "{app}\Heimora.exe"; Description: "Heimora starten"; Flags: nowait postinstall skipifsilent; Check: not IsTestInstall
[Code]
function IsTestInstall: Boolean;
begin
  Result := ExpandConstant('{param:TESTINSTALL|0}') = '1';
end;
