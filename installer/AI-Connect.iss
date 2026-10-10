; AI Connect installer (Inno Setup 6). Build with installer\build-installer.bat after build-all.bat.
#define AppVer "1.2.0"

[Setup]
AppId={{8F3A6C52-4B1E-4D7A-9C21-5E7A1B0D3F44}
AppName=AI Connect for Navisworks
AppVersion={#AppVer}
AppPublisher=Surajprem7
AppPublisherURL=https://github.com/Surajprem7/navisworks-ai-connect
AppSupportURL=https://github.com/Surajprem7/navisworks-ai-connect/issues
DefaultDirName={commonappdata}\AI Connect
DisableDirPage=yes
DisableProgramGroupPage=yes
DefaultGroupName=AI Connect
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
OutputDir=Output
OutputBaseFilename=AI-Connect-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=AI Connect for Navisworks
LicenseFile=..\LICENSE
SetupIconFile=ai-connect.ico

[Files]
Source: "..\mcp-server\server.js"; DestDir: "{app}\mcp-server"; Flags: ignoreversion
Source: "..\add-to-claude-config.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\report-bug.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\report-bug.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\2022\NavisBridge.dll"; DestDir: "{code:PluginDir|2022}"; Flags: ignoreversion; Check: Selected('2022')
Source: "..\dist\2022\Newtonsoft.Json.dll"; DestDir: "{code:PluginDir|2022}"; Flags: ignoreversion; Check: Selected('2022')
Source: "..\dist\2022\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2022}"; Flags: ignoreversion; Check: Selected('2022')
Source: "..\dist\2022\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2022}\en-US"; Flags: ignoreversion; Check: Selected('2022')
Source: "..\dist\2022\Resources\*.png"; DestDir: "{code:PluginDir|2022}\Resources"; Flags: ignoreversion; Check: Selected('2022')
Source: "..\dist\2023\NavisBridge.dll"; DestDir: "{code:PluginDir|2023}"; Flags: ignoreversion; Check: Selected('2023')
Source: "..\dist\2023\Newtonsoft.Json.dll"; DestDir: "{code:PluginDir|2023}"; Flags: ignoreversion; Check: Selected('2023')
Source: "..\dist\2023\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2023}"; Flags: ignoreversion; Check: Selected('2023')
Source: "..\dist\2023\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2023}\en-US"; Flags: ignoreversion; Check: Selected('2023')
Source: "..\dist\2023\Resources\*.png"; DestDir: "{code:PluginDir|2023}\Resources"; Flags: ignoreversion; Check: Selected('2023')
Source: "..\dist\2024\NavisBridge.dll"; DestDir: "{code:PluginDir|2024}"; Flags: ignoreversion; Check: Selected('2024')
Source: "..\dist\2024\Newtonsoft.Json.dll"; DestDir: "{code:PluginDir|2024}"; Flags: ignoreversion; Check: Selected('2024')
Source: "..\dist\2024\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2024}"; Flags: ignoreversion; Check: Selected('2024')
Source: "..\dist\2024\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2024}\en-US"; Flags: ignoreversion; Check: Selected('2024')
Source: "..\dist\2024\Resources\*.png"; DestDir: "{code:PluginDir|2024}\Resources"; Flags: ignoreversion; Check: Selected('2024')
Source: "..\dist\2025\NavisBridge.dll"; DestDir: "{code:PluginDir|2025}"; Flags: ignoreversion; Check: Selected('2025')
Source: "..\dist\2025\Newtonsoft.Json.dll"; DestDir: "{code:PluginDir|2025}"; Flags: ignoreversion; Check: Selected('2025')
Source: "..\dist\2025\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2025}"; Flags: ignoreversion; Check: Selected('2025')
Source: "..\dist\2025\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2025}\en-US"; Flags: ignoreversion; Check: Selected('2025')
Source: "..\dist\2025\Resources\*.png"; DestDir: "{code:PluginDir|2025}\Resources"; Flags: ignoreversion; Check: Selected('2025')
Source: "..\dist\2026\NavisBridge.dll"; DestDir: "{code:PluginDir|2026}"; Flags: ignoreversion; Check: Selected('2026')
Source: "..\dist\2026\Newtonsoft.Json.dll"; DestDir: "{code:PluginDir|2026}"; Flags: ignoreversion; Check: Selected('2026')
Source: "..\dist\2026\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2026}"; Flags: ignoreversion; Check: Selected('2026')
Source: "..\dist\2026\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2026}\en-US"; Flags: ignoreversion; Check: Selected('2026')
Source: "..\dist\2026\Resources\*.png"; DestDir: "{code:PluginDir|2026}\Resources"; Flags: ignoreversion; Check: Selected('2026')
Source: "..\dist\2027\NavisBridge.dll"; DestDir: "{code:PluginDir|2027}"; Flags: ignoreversion; Check: Selected('2027')
Source: "..\dist\2027\Newtonsoft.Json.dll"; DestDir: "{code:PluginDir|2027}"; Flags: ignoreversion; Check: Selected('2027')
Source: "..\dist\2027\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2027}"; Flags: ignoreversion; Check: Selected('2027')
Source: "..\dist\2027\ClaudeRibbon.xaml"; DestDir: "{code:PluginDir|2027}\en-US"; Flags: ignoreversion; Check: Selected('2027')
Source: "..\dist\2027\Resources\*.png"; DestDir: "{code:PluginDir|2027}\Resources"; Flags: ignoreversion; Check: Selected('2027')

[Icons]
Name: "{group}\Report a bug"; Filename: "{app}\report-bug.bat"; WorkingDir: "{app}"
Name: "{group}\AI Connect on GitHub"; Filename: "https://github.com/Surajprem7/navisworks-ai-connect"
Name: "{group}\Check for updates"; Filename: "https://github.com/Surajprem7/navisworks-ai-connect/releases/latest"
Name: "{group}\Uninstall AI Connect"; Filename: "{uninstallexe}"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\add-to-claude-config.ps1"" -ServerJs ""{app}\mcp-server\server.js"" -Quiet"; Flags: runasoriginaluser runhidden; StatusMsg: "Registering AI Connect with Claude Desktop..."

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\add-to-claude-config.ps1"" -Remove -Quiet"; Flags: runhidden; RunOnceId: "RemoveClaudeEntry"

[Code]
var
  Page: TInputOptionWizardPage;
  NavDirs, NavYears, NavNames: array of String;
  OldVersion: String;

function VerPart(const V: String; Idx: Integer): Integer;
var S: String; I, P: Integer;
begin
  S := V;
  for I := 0 to Idx - 1 do
  begin
    P := Pos('.', S);
    if P = 0 then begin Result := 0; Exit; end;
    S := Copy(S, P + 1, Length(S));
  end;
  P := Pos('.', S);
  if P > 0 then S := Copy(S, 1, P - 1);
  Result := StrToIntDef(S, 0);
end;

function CompareVer(const A, B: String): Integer;
var I: Integer;
begin
  Result := 0;
  for I := 0 to 2 do
    if VerPart(A, I) <> VerPart(B, I) then
    begin
      if VerPart(A, I) > VerPart(B, I) then Result := 1 else Result := -1;
      Exit;
    end;
end;

procedure AddInstall(const Product, Year: String);
var Dir: String; N: Integer;
begin
  Dir := ExpandConstant('{commonpf64}') + '\Autodesk\Navisworks ' + Product + ' ' + Year;
  if FileExists(Dir + '\Autodesk.Navisworks.Api.dll') then
  begin
    N := GetArrayLength(NavDirs);
    SetArrayLength(NavDirs, N + 1); SetArrayLength(NavYears, N + 1); SetArrayLength(NavNames, N + 1);
    NavDirs[N] := Dir; NavYears[N] := Year; NavNames[N] := 'Navisworks ' + Product + ' ' + Year;
  end;
end;

function InitializeSetup(): Boolean;
var Y: Integer;
begin
  Result := True;
  if not RegQueryStringValue(HKLM64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3A6C52-4B1E-4D7A-9C21-5E7A1B0D3F44}_is1', 'DisplayVersion', OldVersion) then
    OldVersion := '';
  if (OldVersion <> '') and (CompareVer(OldVersion, '{#AppVer}') > 0) then
    Result := MsgBox('A newer version (' + OldVersion + ') is already installed. Install the older version {#AppVer} anyway?', mbConfirmation, MB_YESNO) = IDYES;
  for Y := 2022 to 2027 do
  begin
    AddInstall('Manage', IntToStr(Y));
    AddInstall('Simulate', IntToStr(Y));
  end;
end;

procedure InitializeWizard();
var I: Integer;
begin
  if OldVersion <> '' then
    if CompareVer(OldVersion, '{#AppVer}') < 0 then
      WizardForm.WelcomeLabel2.Caption := 'This will upgrade AI Connect from version ' + OldVersion + ' to {#AppVer}. Your settings are kept.' + #13#10#13#10 + 'Close Navisworks before continuing.'
    else
      WizardForm.WelcomeLabel2.Caption := 'AI Connect {#AppVer} is already installed. This will repair it.' + #13#10#13#10 + 'Close Navisworks before continuing.'
  else
    WizardForm.WelcomeLabel2.Caption := 'This installs AI Connect {#AppVer} so an AI assistant can work with Autodesk Navisworks (2022-2027).' + #13#10#13#10 + 'Close Navisworks before continuing.';
  Page := CreateInputOptionPage(wpLicense, 'Navisworks versions', 'Choose where to install the AI Connect add-in.',
    'Installed versions found on this PC:', False, False);
  for I := 0 to GetArrayLength(NavDirs) - 1 do
    Page.Add(NavNames[I]);
  for I := 0 to GetArrayLength(NavDirs) - 1 do
    // upgrading: only update versions that already have the add-in; first install: all detected versions
    if OldVersion <> '' then Page.Values[I] := DirExists(NavDirs[I] + '\Plugins\NavisBridge')
    else Page.Values[I] := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var I, C: Integer;
begin
  Result := True;
  if CurPageID = Page.ID then
  begin
    C := 0;
    for I := 0 to GetArrayLength(NavDirs) - 1 do
      if Page.Values[I] then C := C + 1;
    if C = 0 then
    begin
      MsgBox('No Navisworks version selected (2022-2027 Manage or Simulate must be installed).', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
end;

function Selected(Year: String): Boolean;
var I: Integer;
begin
  Result := False;
  for I := 0 to GetArrayLength(NavDirs) - 1 do
    if (NavYears[I] = Year) and Page.Values[I] then begin Result := True; Exit; end;
end;

function PluginDir(Year: String): String;
var I: Integer;
begin
  Result := ExpandConstant('{app}');
  for I := 0 to GetArrayLength(NavDirs) - 1 do
    if (NavYears[I] = Year) and Page.Values[I] then begin Result := NavDirs[I] + '\Plugins\NavisBridge'; Exit; end;
end;

function NavisRunning(): Boolean;
var Code: Integer;
begin
  Result := Exec(ExpandConstant('{cmd}'), '/c tasklist /fi "imagename eq roamer.exe" | find /i "roamer.exe"', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  while NavisRunning() do
    if MsgBox('Navisworks is running. Close it, then click Retry.', mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := 'Installation cancelled because Navisworks is still running.';
      Exit;
    end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then
    if not Exec(ExpandConstant('{cmd}'), '/c where node >nul 2>nul', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      MsgBox('Node.js was not found. Claude needs it to run the AI Connect connector.' + #13#10#13#10 + 'Install it with:  winget install OpenJS.NodeJS.LTS' + #13#10 + 'then run the installer again (or Claude config step) so Claude can find it.', mbInformation, MB_OK);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + 'Next: fully quit Claude (system tray > Quit) and reopen it. Then open Navisworks and click AI Connect > AI Connect on the ribbon.';
end;
