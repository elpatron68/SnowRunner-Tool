; SnowRunner-Tool Inno Setup script (.NET 8)
#pragma include __INCLUDE__ + ";" + ReadReg(HKLM, "Software\Mitrich Software\Inno Download Plugin", "InstallDir")
#include <idp.iss>

#define MyAppName "SnowRunner-Tool"
#define MyAppVersion "1.0.5.4"
#define MyAppPublisher "elpatron68"
#define MyAppURL "https://github.com/elpatron68/SnowRunner-Tool"
#define MyAppExeName "SnowRunner-Tool.exe"
#define MyAppSourceDir "..\SnowRunner-Tool\bin\Release"

[Setup]
AppId={{fbc516d8-771f-414e-b57b-14211d6e0c62}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
LicenseFile=..\copying
OutputDir=setupfiles
OutputBaseFilename=SRT_setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,placement.config"

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
IDP_DownloadFailed=Download of the .NET 8 Desktop Runtime failed. It is required to run SnowRunner-Tool.
IDP_RetryCancel=Click 'Retry' to try downloading the files again, or click 'Cancel' to terminate setup.
InstallingDotNetRuntime=Installing .NET 8 Desktop Runtime. This might take a few minutes...
DotNetRuntimeFailedToLaunch=Failed to launch the .NET Desktop Runtime installer with error "%1". Please fix the error then run this installer again.
DotNetRuntimeFailed1602=.NET Desktop Runtime installation was cancelled. This installation can continue, but the application may not run until the runtime is installed.
DotNetRuntimeFailed1603=A fatal error occurred while installing the .NET Desktop Runtime. Please fix the error, then run the installer again.
DotNetRuntimeFailedOther=The .NET Desktop Runtime installer exited with an unexpected status code "%1".

[Code]
var
  requiresRestart: boolean;

function DotNetDesktopRuntimeIsMissing(): Boolean;
var
  FindRec: TFindRec;
  SharedDir: String;
begin
  Result := True;
  SharedDir := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(SharedDir) then
    Exit;

  if FindFirst(SharedDir + '\8.*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) then
      begin
        Result := False;
        Break;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

procedure InitializeWizard;
begin
  if DotNetDesktopRuntimeIsMissing() then
  begin
    idpAddFile('https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe', ExpandConstant('{tmp}\windowsdesktop-runtime-8.exe'));
    idpDownloadAfter(wpReady);
  end;
end;

function InstallDotNetRuntime(): String;
var
  StatusText: string;
  ResultCode: Integer;
begin
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := CustomMessage('InstallingDotNetRuntime');
  WizardForm.ProgressGauge.Style := npbstMarquee;
  try
    if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime-8.exe'), '/install /passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := FmtMessage(CustomMessage('DotNetRuntimeFailedToLaunch'), [SysErrorMessage(ResultCode)]);
    end
    else
    begin
      case ResultCode of
        0: begin
        end;
        1602: begin
          MsgBox(CustomMessage('DotNetRuntimeFailed1602'), mbInformation, MB_OK);
        end;
        1603: begin
          Result := CustomMessage('DotNetRuntimeFailed1603');
        end;
        1641, 3010: begin
          requiresRestart := True;
        end;
        else begin
          MsgBox(FmtMessage(CustomMessage('DotNetRuntimeFailedOther'), [IntToStr(ResultCode)]), mbError, MB_OK);
        end;
      end;
    end;
  finally
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.ProgressGauge.Style := npbstNormal;
    DeleteFile(ExpandConstant('{tmp}\windowsdesktop-runtime-8.exe'));
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if DotNetDesktopRuntimeIsMissing() then
  begin
    Result := InstallDotNetRuntime();
  end;
end;

function NeedRestart(): Boolean;
begin
  Result := requiresRestart;
end;
