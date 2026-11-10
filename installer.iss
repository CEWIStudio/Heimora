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

function CompatibilityLabel(Index: Integer): String;
var Data: String; I: Integer;
begin
  case Index of
    0: Data := '44472D52656D6F7465';
    1: Data := '44472052656D6F7465';
    2: Data := '52434D4D616E61676572';
    3: Data := '52656D6F746520436F6E6E656374696F6E204D616E61676572';
  end;
  Result := '';
  for I := 0 to (Length(Data) div 2) - 1 do
    Result := Result + Chr(StrToInt('$' + Copy(Data, I * 2 + 1, 2)));
end;

function PlainDirectoryTree(Path: String): Boolean;
var Entry, RootEntry: TFindRec;
begin
  Result := True;
  if FindFirst(Path, RootEntry) then begin
    try
      if (RootEntry.Attributes and $400) <> 0 then begin Result := False; Exit; end;
    finally FindClose(RootEntry); end;
  end else begin Result := False; Exit; end;
  if FindFirst(AddBackslash(Path) + '*', Entry) then begin
    try
      repeat
        if (Entry.Name <> '.') and (Entry.Name <> '..') then begin
          if (Entry.Attributes and $400) <> 0 then begin Result := False; Exit; end;
          if ((Entry.Attributes and $10) <> 0) and
             (not PlainDirectoryTree(AddBackslash(Path) + Entry.Name)) then begin Result := False; Exit; end;
        end;
      until not FindNext(Entry);
    finally FindClose(Entry); end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var I, J: Integer; LabelText, GroupPath, PreviousPath, ExeName, BinaryHash: String; Owned: Boolean;
begin
  if (CurStep <> ssPostInstall) or IsTestInstall then Exit;
  for I := 0 to 3 do begin
    LabelText := CompatibilityLabel(I);
    DeleteFile(ExpandConstant('{autodesktop}\') + LabelText + '.lnk');
    GroupPath := ExpandConstant('{autoprograms}\') + LabelText;
    DeleteFile(GroupPath + '\' + LabelText + '.lnk');
    DeleteFile(GroupPath + '\Handleiding en beveiliging.lnk');
    RemoveDir(GroupPath);
    PreviousPath := ExpandConstant('{autopf}\') + LabelText;
    Owned := False;
    for J := 0 to 3 do begin
      ExeName := CompatibilityLabel(J);
      StringChangeEx(ExeName, ' ', '', True); StringChangeEx(ExeName, '-', '', True);
      if FileExists(PreviousPath + '\' + ExeName + '.exe') then begin
        BinaryHash := GetSHA256OfFile(PreviousPath + '\' + ExeName + '.exe');
        if (CompareText(BinaryHash, 'EFB3F20ADEB8B4B7F5726CCC46412B5AA82A27E0708C7F1C1F34BC840B12FD4D') = 0) or
           (CompareText(BinaryHash, '43C401AA88AEF4274C9F7FB796EBE286878719F3D3627542CEB68950FE5943C5') = 0) then Owned := True;
      end;
    end;
    if Owned and (CompareText(ExtractFileDir(PreviousPath), ExpandConstant('{autopf}')) = 0) and PlainDirectoryTree(PreviousPath) then
      if not DelTree(PreviousPath, True, True, True) then
        MsgBox('Heimora is geïnstalleerd. Sluit alle eerdere appinstanties en voer de installer opnieuw uit om de overgebleven programmabestanden op te ruimen.', mbInformation, MB_OK);
  end;
end;
