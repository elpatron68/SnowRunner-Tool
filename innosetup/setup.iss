; Script generated by the Inno Script Studio Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!
#pragma include __INCLUDE__ + ";" + ReadReg(HKLM, "Software\Mitrich Software\Inno Download Plugin", "InstallDir")
#include <idp.iss>
#define MyAppName "SnowRunner-Tool"
#define MyAppVersion "1.0.5.3"
#define MyAppPublisher "elpatron68"
#define MyAppURL "https://github.com/elpatron68/SnowRunner-Tool"
#define MyAppExeName "SnowRunner-Tool.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{fbc516d8-771f-414e-b57b-14211d6e0c62}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
LicenseFile=C:\Users\mbusc\source\repos\SnowRunner-Tool\copying
OutputDir=C:\Users\mbusc\source\repos\SnowRunner-Tool\innosetup\setupfiles
OutputBaseFilename=SRT_setup
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\SnowRunner-Tool\bin\Release\SnowRunner-Tool.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SnowRunner-Tool\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SnowRunner-Tool\bin\Release\app.manifest"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SnowRunner-Tool\bin\Release\de\System.Runtime.WindowsRuntime.resources.dll"; DestDir: "{app}\de"; Flags: ignoreversion
Source: "..\SnowRunner-Tool\placement.config"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "..\SnowRunner-Tool\bin\Release\copying"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SnowRunner-Tool\bin\Release\Readme.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SnowRunner-Tool\bin\Release\SnowRunner-Tool.exe.config"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "..\SnowRunner-Tool\bin\Release\Windows.winmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SnowRunner-Tool\bin\Release\Changelog.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Project on GitHub"; Filename: "{#MyAppURL}"
Name: "{group}\Donate a coffee"; Filename: "https://www.paypal.com/donate/?hosted_button_id=4HC7YCMXQK3N8"
Name: "{group}\Show Changelog"; Filename: "notepad.exe"; Parameters: "{app}\changelog.md"
Name: "{group}\Show README"; Filename: "notepad.exe"; Parameters: "{app}\readme.md"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\*"
Type: filesandordirs; Name: "{localappdata}\SRT\*"
Type: filesandordirs; Name: "{localappdata}\SnowRunner_Tool\*"

[CustomMessages]
IDP_DownloadFailed=Download of .NET Framework 4.7.2 failed. .NET Framework 4.7 is required to run SnowRunner-Tool.
IDP_RetryCancel=Click 'Retry' to try downloading the files again, or click 'Cancel' to terminate setup.
InstallingDotNetFramework=Installing .NET Framework 4.7.2. This might take a few minutes...
DotNetFrameworkFailedToLaunch=Failed to launch .NET Framework Installer with error "%1". Please fix the error then run this installer again.
DotNetFrameworkFailed1602=.NET Framework installation was cancelled. This installation can continue, but be aware that this application may not run unless the .NET Framework installation is completed successfully.
DotNetFrameworkFailed1603=A fatal error occurred while installing the .NET Framework. Please fix the error, then run the installer again.
DotNetFrameworkFailed5100=Your computer does not meet the requirements of the .NET Framework. Please consult the documentation.
DotNetFrameworkFailedOther=The .NET Framework installer exited with an unexpected status code "%1". Please review any other messages shown by the installer to determine whether the installation completed successfully, and abort this installation and fix the problem if it did not.

[Code]
var
  requiresRestart: boolean;

function NetFrameworkIsMissing(): Boolean;
var
  bSuccess: Boolean;
  regVersion: Cardinal;
begin
  Result := True;

  bSuccess := RegQueryDWordValue(HKLM, 'Software\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', regVersion);
  if (True = bSuccess) and (regVersion >= 461308) then begin
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  if NetFrameworkIsMissing() then
  begin
    idpAddFile('http://go.microsoft.com/fwlink/?LinkId=863262', ExpandConstant('{tmp}\NetFrameworkInstaller.exe'));
    idpDownloadAfter(wpReady);
  end;
end;

function InstallFramework(): String;
var
  StatusText: string;
  ResultCode: Integer;
begin
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := CustomMessage('InstallingDotNetFramework');
  WizardForm.ProgressGauge.Style := npbstMarquee;
  try
    if not Exec(ExpandConstant('{tmp}\NetFrameworkInstaller.exe'), '/passive /norestart /showrmui /showfinalerror', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := FmtMessage(CustomMessage('DotNetFrameworkFailedToLaunch'), [SysErrorMessage(resultCode)]);
    end
    else
    begin
      // See https://msdn.microsoft.com/en-us/library/ee942965(v=vs.110).aspx#return_codes
      case resultCode of
        0: begin
          // Successful
        end;
        1602 : begin
          MsgBox(CustomMessage('DotNetFrameworkFailed1602'), mbInformation, MB_OK);
        end;
        1603: begin
          Result := CustomMessage('DotNetFrameworkFailed1603');
        end;
        1641: begin
          requiresRestart := True;
        end;
        3010: begin
          requiresRestart := True;
        end;
        5100: begin
          Result := CustomMessage('DotNetFrameworkFailed5100');
        end;
        else begin
          MsgBox(FmtMessage(CustomMessage('DotNetFrameworkFailedOther'), [IntToStr(resultCode)]), mbError, MB_OK);
        end;
      end;
    end;
  finally
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.ProgressGauge.Style := npbstNormal;
    
    DeleteFile(ExpandConstant('{tmp}\NetFrameworkInstaller.exe'));
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  // 'NeedsRestart' only has an effect if we return a non-empty string, thus aborting the installation.
  // If the installers indicate that they want a restart, this should be done at the end of installation.
  // Therefore we set the global 'restartRequired' if a restart is needed, and return this from NeedRestart()

  if NetFrameworkIsMissing() then
  begin
    Result := InstallFramework();
  end;
end;

function NeedRestart(): Boolean;
begin
  Result := requiresRestart;
end;
