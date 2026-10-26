; Build with tools/Build-Installer.ps1. PreviewOnly removes the entire payload and
; every installation action at compile time; it is safe beside a running app.
#ifndef PreviewOnly
  #define PreviewOnly "0"
#endif
#ifndef TestBuild
  #define TestBuild "0"
#endif
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef OutputPath
  #error OutputPath is required
#endif
#ifndef OutputName
  #error OutputName is required
#endif
#define RepoRoot AddBackslash(SourcePath) + "..\.."

[Setup]
#if TestBuild == "1"
AppId=DesktopBubbles.InstallerFixture
DefaultDirName={#TestRoot}\Program
CreateUninstallRegKey=no
#else
AppId={{A47AA47E-3E32-4A89-A51B-407DFA0A0902}
DefaultDirName={localappdata}\Programs\DesktopBubbles
#endif
#if PreviewOnly == "1"
AppName=DesktopBubbles Setup-Vorschau
CreateAppDir=no
Uninstallable=no
CreateUninstallRegKey=no
#else
AppName=DesktopBubbles
UninstallDisplayIcon={app}\DesktopBubbles.exe
#endif
AppVersion={#AppVersion}
AppPublisher=Hendrik4K
AppPublisherURL=https://github.com/Hendrik4K/DesktopBubbles
AppSupportURL=https://github.com/Hendrik4K/DesktopBubbles/issues
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
UsePreviousAppDir=no
UsePreviousGroup=no
UsePreviousTasks=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableWelcomePage=yes
LicenseFile={#RepoRoot}\LICENSE
WizardStyle=modern dynamic windows11
WizardSizePercent=110,110
WizardSmallImageFile={#RepoRoot}\src\DesktopBubbles\Assets\DesktopBubbles.png
WizardSmallImageFileDynamicDark={#RepoRoot}\src\DesktopBubbles\Assets\DesktopBubbles.png
SetupIconFile={#RepoRoot}\src\DesktopBubbles\Assets\DesktopBubbles.ico
CloseApplications=no
RestartApplications=no
AlwaysRestart=no
Compression=lzma2/fast
SolidCompression=yes
OutputDir={#OutputPath}
OutputBaseFilename={#OutputName}
UninstallDisplayName=DesktopBubbles

[Languages]
Name: german; MessagesFile: compiler:Languages\German.isl

[Tasks]
Name: desktopicon; Description: Desktop-Verknüpfung erstellen; Flags: unchecked

#if PreviewOnly != "1"
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "data\*"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: NotPreview

[Icons]
#if TestBuild == "1"
Name: "{#TestRoot}\StartMenu\DesktopBubbles"; Filename: "{app}\DesktopBubbles.exe"; WorkingDir: "{app}"; Check: NotPreview
Name: "{#TestRoot}\Desktop\DesktopBubbles"; Filename: "{app}\DesktopBubbles.exe"; WorkingDir: "{app}"; Tasks: desktopicon; Check: NotPreview
#else
Name: "{userprograms}\DesktopBubbles"; Filename: "{app}\DesktopBubbles.exe"; WorkingDir: "{app}"; Check: NotPreview
Name: "{userdesktop}\DesktopBubbles"; Filename: "{app}\DesktopBubbles.exe"; WorkingDir: "{app}"; Tasks: desktopicon; Check: NotPreview
#endif

[Run]
Filename: "{app}\DesktopBubbles.exe"; Description: DesktopBubbles starten; Flags: nowait postinstall skipifsilent; Check: NotPreview
#endif

[Code]
var
  WelcomePage: TOutputMsgWizardPage;
  DataPage: TOutputMsgMemoWizardPage;
  PreviewLabel: TNewStaticText;
  BackupDirectory: String;
  BackupMade: Boolean;

function FileAttributes(const FileName: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function OpenProgramFile(const FileName: String; Access, Sharing, Security, Disposition, Flags, Template: LongWord): LongWord;
  external 'CreateFileW@kernel32.dll stdcall';
function CloseProgramFile(Handle: LongWord): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function MapInvariantCase(const LocaleName: String; Flags: LongWord; const Source: String;
  SourceLength: Integer; Destination: String; DestinationLength: Integer;
  VersionInfo, Reserved, SortHandle: LongWord): Integer;
  external 'LCMapStringEx@kernel32.dll stdcall';

function InvariantUpper(const Source: String): String;
var Needed: Integer;
begin
  { Inno Uppercase handles ASCII only; match the app's Unicode path hashing. }
  Result := '';
  Needed := MapInvariantCase('', $200, Source, Length(Source), Result, 0, 0, 0, 0);
  if Needed <= 0 then RaiseException('Der Programmpfad kann nicht geprüft werden.');
  SetLength(Result, Needed);
  if MapInvariantCase('', $200, Source, Length(Source), Result, Needed, 0, 0, 0) = 0 then
    RaiseException('Der Programmpfad kann nicht geprüft werden.');
end;

function IsPreview: Boolean;
begin
  Result := {#PreviewOnly} = 1;
end;

function NotPreview: Boolean;
begin
  Result := not IsPreview;
end;

function InstallDirectory: String;
begin
#if TestBuild == "1"
  Result := '{#TestRoot}\Program';
#else
  Result := ExpandConstant('{localappdata}\Programs\DesktopBubbles');
#endif
end;

function BackupRoot: String;
begin
#if TestBuild == "1"
  Result := '{#TestRoot}\Backups';
#else
  Result := ExpandConstant('{localappdata}\DesktopBubbles\InstallerBackups');
#endif
end;

function AppRunning: Boolean;
var Hash, ProgramFile: String; Handle: LongWord;
begin
  Hash := Uppercase(GetSHA256OfString(Utf8Encode(InvariantUpper(InstallDirectory + '\data'))));
  Result := CheckForMutexes('Local\DesktopBubbles-' + Copy(Hash, 1, 16));
  { Also protect the executable when it was launched with a different --data-dir.
    OPEN_EXISTING requests write access but never writes or truncates the file. }
  ProgramFile := InstallDirectory + '\DesktopBubbles.exe';
  if not Result and FileExists(ProgramFile) then
  begin
    Handle := OpenProgramFile(ProgramFile, $40000000, 7, 0, 3, 0, 0);
    Result := Handle = $FFFFFFFF;
    if not Result then CloseProgramFile(Handle);
  end;
end;

procedure CopyDataTree(const Source, Destination: String; var Count: Integer);
var Entry: TFindRec; FromPath, ToPath: String;
begin
  if not ForceDirectories(Destination) then RaiseException('Der Sicherungsordner kann nicht erstellt werden.');
  if FindFirst(Source + '\*', Entry) then
  try
    repeat
      if (Entry.Name <> '.') and (Entry.Name <> '..') then
      begin
        FromPath := Source + '\' + Entry.Name; ToPath := Destination + '\' + Entry.Name;
        if (Entry.Attributes and $400) <> 0 then RaiseException('Der Datenordner enthält einen Dateisystem-Link. Bitte die Daten vor dem Update manuell sichern.');
        if (Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then CopyDataTree(FromPath, ToPath, Count)
        else begin
          if not FileCopy(FromPath, ToPath, True) then RaiseException('Eine Datendatei kann nicht gesichert werden: ' + Entry.Name);
          if GetSHA256OfFile(FromPath) <> GetSHA256OfFile(ToPath) then RaiseException('Die Datensicherung konnte nicht bestätigt werden.');
          Count := Count + 1;
        end;
      end;
    until not FindNext(Entry);
  finally FindClose(Entry); end;
end;

procedure VerifyDataTree(const Source, Destination: String; var Count: Integer);
var Entry: TFindRec; FromPath, ToPath: String;
begin
  if FindFirst(Source + '\*', Entry) then
  try
    repeat
      if (Entry.Name <> '.') and (Entry.Name <> '..') then
      begin
        FromPath := Source + '\' + Entry.Name; ToPath := Destination + '\' + Entry.Name;
        if (Entry.Attributes and $400) <> 0 then RaiseException('Der Datenordner wurde während der Sicherung geändert.');
        if (Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then VerifyDataTree(FromPath, ToPath, Count)
        else begin
          if not FileExists(ToPath) then RaiseException('Die Daten wurden während der Sicherung geändert.');
          if GetSHA256OfFile(FromPath) <> GetSHA256OfFile(ToPath) then RaiseException('Die Daten wurden während der Sicherung geändert.');
          Count := Count + 1;
        end;
      end;
    until not FindNext(Entry);
  finally FindClose(Entry); end;
end;

procedure InitializeWizard;
var Intro: String;
begin
  Intro := 'Viele Verknüpfungen. Eine Bubble.' + #13#10#13#10 +
    'DesktopBubbles bringt App-Ordner auf deinen Windows-Desktop.' + #13#10#13#10 +
    'Die Einrichtung installiert die App nur für dein Benutzerkonto und legt einen Eintrag im Startmenü an. Eine Desktop-Verknüpfung ist optional.';
  if IsPreview then Intro := 'VORSCHAU – es wird nichts installiert.' + #13#10#13#10 + Intro;
  WelcomePage := CreateOutputMsgPage(wpWelcome, 'Mehr Platz für deinen Desktop.', 'DesktopBubbles ' + '{#AppVersion}', Intro);
  DataPage := CreateOutputMsgMemoPage(wpSelectTasks, 'Deine Einrichtung bleibt erhalten.', 'Programm und persönliche Daten', '', '');
  if IsPreview then
  begin
    WizardForm.Caption := 'DesktopBubbles Setup – Vorschau';
    WizardForm.CancelButton.Caption := 'Schließen';
    PreviewLabel := TNewStaticText.Create(WizardForm);
    PreviewLabel.Parent := WizardForm;
    PreviewLabel.Left := ScaleX(16); PreviewLabel.Top := WizardForm.NextButton.Top + ScaleY(6);
    PreviewLabel.Width := ScaleX(235); PreviewLabel.Height := ScaleY(20);
    PreviewLabel.Caption := 'VORSCHAU · keine Installation'; PreviewLabel.Font.Style := [fsBold];
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
var Existing: String;
begin
  if IsPreview then WizardForm.CancelButton.Caption := 'Schließen';
  if CurPageID = DataPage.ID then
  begin
    if FileExists(InstallDirectory + '\data\groups.json') then Existing := 'Vorhandene Gruppen und Einstellungen gefunden. Sie bleiben beim Update erhalten.'
    else Existing := 'An diesem Ort sind noch keine Gruppen gespeichert. Andere portable Ordner werden nicht automatisch zusammengeführt.';
    DataPage.RichEditViewer.Text := Existing + #13#10#13#10 + 'Programm:' + #13#10 + InstallDirectory + #13#10#13#10 +
      'Deine Gruppen, Einstellungen und gesicherten Verknüpfungen:' + #13#10 + InstallDirectory + '\data' + #13#10#13#10 +
      'Vor einem Update wird dieser Datenordner vollständig kopiert und geprüft. Eine Deinstallation entfernt die Programmdateien, behält aber deine Daten und Sicherungen.';
  end;
  if IsPreview and (CurPageID = wpReady) then WizardForm.NextButton.Caption := 'Vorschau beenden';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if IsPreview and (CurPageID = wpReady) then
  begin
    Log('PREVIEW_INSTALL_BLOCKED'); Result := False;
    if not WizardSilent then WizardForm.Close;
  end;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if IsPreview then Confirm := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if IsPreview then Result := 'Vorschau: Installation ist gesperrt.'
  else if CompareText(RemoveBackslash(ExpandConstant('{app}')), InstallDirectory) <> 0 then
    Result := 'Dieser Installer verwendet den festen Benutzer-Programmordner. Ein abweichendes Ziel wurde nicht übernommen.'
  else if AppRunning then Result := 'Bitte DesktopBubbles über das Tray-Menü → Beenden schließen und erneut versuchen. Die Programmdatei muss beschreibbar sein.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Count, Verified: Integer; DataDirectory: String;
begin
  if CurStep = ssInstall then
  begin
    if IsPreview then RaiseException('Vorschau: Installation ist gesperrt.');
    if AppRunning then RaiseException('DesktopBubbles läuft noch. Die Installation wurde abgebrochen.');
    DataDirectory := InstallDirectory + '\data';
    if DirExists(DataDirectory) and not BackupMade then
    begin
      if (FileAttributes(DataDirectory) and $400) <> 0 then RaiseException('Der Datenordner ist ein Dateisystem-Link.');
      BackupDirectory := BackupRoot + '\' + GetDateTimeString('yyyymmdd-hhnnss', '-', ':');
      if DirExists(BackupDirectory) then RaiseException('Dieser Sicherungsordner existiert bereits. Bitte kurz warten und erneut versuchen.');
      Count := 0; Verified := 0; CopyDataTree(DataDirectory, BackupDirectory, Count);
      VerifyDataTree(DataDirectory, BackupDirectory, Verified);
      if Count <> Verified then RaiseException('Die Daten wurden während der Sicherung geändert.');
      BackupMade := True; Log('DATA_BACKUP_VERIFIED: ' + IntToStr(Count) + ' files at ' + BackupDirectory);
    end;
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := not AppRunning;
  if not Result then MsgBox('Bitte DesktopBubbles über Tray → Beenden schließen und die Deinstallation erneut starten.', mbInformation, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var CurrentCommand, OwnCommand: String;
begin
#if TestBuild != "1" && PreviewOnly != "1"
  if CurUninstallStep = usUninstall then
  begin
    OwnCommand := '"' + InstallDirectory + '\DesktopBubbles.exe" --background --data-dir "' + InstallDirectory + '\data" --portable';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DesktopBubbles', CurrentCommand) then
      if CompareText(CurrentCommand, OwnCommand) = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DesktopBubbles');
  end;
#endif
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := 'DesktopBubbles {#AppVersion}' + NewLine + NewLine + 'Programm: ' + InstallDirectory + NewLine +
    'Startmenü und Windows-Suche: DesktopBubbles' + NewLine + NewLine +
    'Vorhandene Daten bleiben erhalten. Vor einem Update wird eine geprüfte Sicherung erstellt.' + NewLine + NewLine + MemoTasksInfo;
  if IsPreview then Result := 'VORSCHAU – keine Installation, keine Verknüpfungen, keine Änderungen an deinen Gruppen.' + NewLine + NewLine + Result;
end;
