; ThixTunnel - Windows setup (Inno Setup 6).
;
; Installs for the current user only (no administrator rights): %LocalAppData%\Programs\ThixTunnel.
; Program files and user data are separate: an update or uninstall never touches %AppData%\ThixTunnel
; unless the user explicitly chooses to delete the data while uninstalling.
;
; The same setup is the update: ThixTunnel downloads it and runs it with /VERYSILENT /UPDATE;
; afterwards ThixTunnel is started again in the tray.
;
; Built by CI (.github/workflows/build.yml):
;   iscc /DAppVersion=0.1.42-20261104 /DAppNumericVersion=0.1.42 /DSourceDir=..\publish installer\ThixTunnel.iss

#ifndef AppVersion
  #define AppVersion "local"
#endif
#ifndef AppNumericVersion
  #define AppNumericVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "ThixTunnel"
#define AppExe "ThixTunnel.exe"
#define AppId "{{5E1F3C1B-7A41-4C4E-9B7E-2D6A1C0F7B21}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Matthias Schmoldt
AppPublisherURL=https://github.com/Real-TTX/ThixTunnel
AppSupportURL=https://github.com/Real-TTX/ThixTunnel/issues
VersionInfoVersion={#AppNumericVersion}
VersionInfoProductVersion={#AppNumericVersion}
VersionInfoProductTextVersion={#AppVersion}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=ThixTunnelSetup
SetupIconFile=..\src\ThixTunnel\Assets\thixtunnel.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
MinVersion=10.0.17763

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
german.AutostartTask=Bei der Windows-Anmeldung starten (im Tray)
english.AutostartTask=Start when signing in to Windows (in the tray)
german.ShortcutsGroup=Verknüpfungen und Start:
english.ShortcutsGroup=Shortcuts and start:
german.OpenSshMissing=Der Windows OpenSSH-Client (ssh.exe) wurde nicht gefunden.%n%nThixTunnel benötigt ihn für die Verbindungen. Installieren unter: Einstellungen › System › Optionale Features › „OpenSSH-Client“.%n%nDie Installation wird trotzdem fortgesetzt.
english.OpenSshMissing=The Windows OpenSSH client (ssh.exe) was not found.%n%nThixTunnel needs it for its connections. Install it under Settings › System › Optional features › "OpenSSH Client".%n%nSetup continues anyway.
german.DeleteUserData=Sollen auch die gespeicherten SSH-Profile, Tunnelrouten und Einstellungen gelöscht werden?%n%n(Bei „Nein“ bleiben sie für eine spätere Installation erhalten.)
english.DeleteUserData=Also delete the saved SSH profiles, tunnel routes and settings?%n%n(Choose "No" to keep them for a later installation.)

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:ShortcutsGroup}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:ShortcutsGroup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value the application writes for "Bei Windows-Anmeldung starten" (AutostartService).
; Only on a first installation: an update must not switch the autostart on again after the user switched it off.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"" --autostart"; Tasks: autostart; Check: IsFirstInstallation

[Run]
; Interactive installation: offer to start ThixTunnel.
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Silent installation (update): start ThixTunnel again, in the tray.
Filename: "{app}\{#AppExe}"; Parameters: "--tray"; Flags: nowait skipifnotsilent

[Code]
var
  WasInstalledBefore: Boolean;

function UninstallKey: String;
begin
  Result := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1';
end;

function IsFirstInstallation: Boolean;
begin
  Result := not WasInstalledBefore;
end;

function InitializeSetup: Boolean;
begin
  WasInstalledBefore := RegKeyExists(HKCU, UninstallKey);
  if (not WizardSilent) and (not FileExists(ExpandConstant('{sys}\OpenSSH\ssh.exe'))) then
    MsgBox(CustomMessage('OpenSshMissing'), mbInformation, MB_OK);
  Result := True;
end;

function InitializeUninstall: Boolean;
var
  ResultCode: Integer;
begin
  { ThixTunnel ends its tunnels when it is closed; make sure it is not running. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    { The autostart value may have been written by the application, not by setup. }
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
  end;

  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
  begin
    if MsgBox(CustomMessage('DeleteUserData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\{#AppName}'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\{#AppName}'), True, True, True);
    end;
  end;
end;
