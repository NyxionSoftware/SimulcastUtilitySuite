#define ProductName "Simulcast Utility Setup"
#define PublisherName "NyxionSoftware"
#define ProjectRoot "..\.."
#include "Version.iss"

#define BCM_SETSHIELD 0x160C

[Setup]
AppId={{B3D2D1A5-DFF2-48AC-812B-16DC75168D6D}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppPublisher={#PublisherName}
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableWelcomePage=no
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=..\Output
OutputBaseFilename=SimulcastUtilitySuite-v{#ProductVersion}-win-x64-setup
SetupIconFile={#ProjectRoot}\SimulcastUtility\App-Icon.ico
WizardSmallImageFile=App-Icon.bmp
WizardSmallImageBackColor=$251D19
WizardImageFile=WizardImage.bmp
WizardImageBackColor=$171210
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion={#ProductVersion}
VersionInfoCompany={#PublisherName}
VersionInfoDescription=Simulcast Utility Installer
VersionInfoProductName={#ProductName}
VersionInfoProductVersion={#ProductVersion}

[Files]
Source: "..\Output\Packages\SimulcastUtilityUserSetup.exe"; Flags: dontcopy
Source: "..\Output\Packages\SimulcastUtilityWorkstationSetup.exe"; Flags: dontcopy
Source: "ValidateReceiverImport.ps1"; Flags: dontcopy

[Run]
Filename: "{localappdata}\Programs\Simulcast Utility\SimulcastUtility.exe"; Description: "Run Simulcast Utility"; WorkingDir: "{localappdata}\Programs\Simulcast Utility"; Flags: nowait postinstall skipifsilent; Check: IsUserInstallSelected
Filename: "{commonpf64}\NyxionSoftware\Simulcast Utility\SimulcastUtility.exe"; Description: "Run Simulcast Utility"; WorkingDir: "{commonpf64}\NyxionSoftware\Simulcast Utility"; Flags: nowait postinstall skipifsilent; Check: IsWorkstationInstallSelected

[Code]
#include "SimulcastTheme.iss"

var
  InstallScopePage: TInputOptionWizardPage;
  ReceiverImportPage: TInputFileWizardPage;

function IsUserInstallSelected: Boolean;
begin
  Result := Assigned(InstallScopePage) and (InstallScopePage.SelectedValueIndex = 0);
end;

function IsWorkstationInstallSelected: Boolean;
begin
  Result := Assigned(InstallScopePage) and (InstallScopePage.SelectedValueIndex = 1);
end;

procedure UpdateNextButtonElevationIcon;
begin
  if IsWorkstationInstallSelected then
    SendMessage(WizardForm.NextButton.Handle, {#BCM_SETSHIELD}, 0, 1)
  else
    SendMessage(WizardForm.NextButton.Handle, {#BCM_SETSHIELD}, 0, 0);
end;

procedure InstallScopeSelectionChanged(Sender: TObject);
begin
  UpdateNextButtonElevationIcon;
end;

procedure InitializeWizard;
var
  DefaultReceiverImportPath: String;
begin
  ApplySimulcastTheme;

  InstallScopePage := CreateInputOptionPage(wpWelcome,
    'Choose who can use Simulcast Utility',
    'Select an installation scope',
    'Install for your Windows account without administrator approval, or install for everyone who uses this workstation.',
    True, False);
  InstallScopePage.Add('Only for me - installs without administrator approval');
  InstallScopePage.Add('Everyone on this workstation - requires administrator approval');
  InstallScopePage.SelectedValueIndex := 0;
  InstallScopePage.CheckListBox.OnClickCheck := @InstallScopeSelectionChanged;
  InstallScopePage.Surface.Color := SimulcastWindowColor;
  InstallScopePage.CheckListBox.Color := SimulcastWindowColor;
  InstallScopePage.CheckListBox.Font.Color := SimulcastTextColor;

  ReceiverImportPage := CreateInputFilePage(InstallScopePage.ID,
    'Import configured receivers',
    'Select an optional receivers.json file',
    'Select a receivers.json file exported from Simulcast Utility, or leave this field blank to start without importing receivers. An import replaces the receiver configuration for this Windows account.');
  ReceiverImportPage.Add('Receiver configuration file:',
    'Receiver configuration (receivers.json)|receivers.json|JSON files (*.json)|*.json|All files (*.*)|*.*',
    '.json');
  ReceiverImportPage.Surface.Color := SimulcastWindowColor;
  ReceiverImportPage.PromptLabels[0].Font.Color := SimulcastMutedTextColor;
  ReceiverImportPage.Edits[0].Color := SimulcastRaisedColor;
  ReceiverImportPage.Edits[0].Font.Color := SimulcastTextColor;
  DefaultReceiverImportPath := ExpandConstant('{src}\receivers.json');

  if FileExists(DefaultReceiverImportPath) then
    ReceiverImportPage.Values[0] := DefaultReceiverImportPath;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  UpdateNextButtonElevationIcon;
end;

function GetReceiverImportValidationCode(const FileName: String): Integer;
var
  Parameters: String;
  ResultCode: Integer;
  ValidatorPath: String;
begin
  ExtractTemporaryFile('ValidateReceiverImport.ps1');
  ValidatorPath := ExpandConstant('{tmp}\ValidateReceiverImport.ps1');
  Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ' + AddQuotes(ValidatorPath) + ' -InputFile ' + AddQuotes(FileName);
  Result := 4;

  if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ImportPath: String;
  ValidationCode: Integer;
begin
  Result := True;

  if CurPageID <> ReceiverImportPage.ID then
    Exit;

  ImportPath := Trim(ReceiverImportPage.Values[0]);

  if ImportPath = '' then
    Exit;

  if not FileExists(ImportPath) then
  begin
    MsgBox('The selected receiver configuration file does not exist.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  ValidationCode := GetReceiverImportValidationCode(ImportPath);

  case ValidationCode of
    0: Result := True;
    1: MsgBox('The selected receiver configuration contains invalid JSON.', mbError, MB_OK);
    2: MsgBox('The selected receiver configuration must contain at least one receiver.', mbError, MB_OK);
    3: MsgBox('The selected file contains an invalid receiver. Every receiver requires a unique ID, name, numeric receiver ID, and IPv4 address.', mbError, MB_OK);
  else
    MsgBox('The selected receiver configuration could not be validated.', mbError, MB_OK);
  end;

  if ValidationCode <> 0 then
    Result := False;
end;

function ImportReceiverConfiguration(var FailureReason: String): Boolean;
var
  DestinationDirectory: String;
  DestinationPath: String;
  ImportPath: String;
begin
  Result := True;
  ImportPath := Trim(ReceiverImportPage.Values[0]);

  if ImportPath = '' then
    Exit;

  DestinationDirectory := ExpandConstant('{localappdata}\SimulcastUtility');
  DestinationPath := DestinationDirectory + '\receivers.json';

  if not ForceDirectories(DestinationDirectory) then
  begin
    FailureReason := 'The Simulcast Utility configuration directory could not be created.';
    Result := False;
    Exit;
  end;

  if CompareText(ExpandFileName(ImportPath), ExpandFileName(DestinationPath)) <> 0 then
  begin
    if not FileCopy(ImportPath, DestinationPath, False) then
    begin
      FailureReason := 'Simulcast Utility was installed, but receivers.json could not be imported.';
      Result := False;
    end;
  end;
end;

function InstallSelectedPackage(var FailureReason: String): Boolean;
var
  PackagePath: String;
  ResultCode: Integer;
begin
  Result := False;

  if IsUserInstallSelected then
  begin
    ExtractTemporaryFile('SimulcastUtilityUserSetup.exe');
    PackagePath := ExpandConstant('{tmp}\SimulcastUtilityUserSetup.exe');

    if not Exec(PackagePath, '/SILENT /SUPPRESSMSGBOXES /NORESTART /SP-', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      FailureReason := 'The Simulcast Utility per-user installer could not be started.';
      Exit;
    end;
  end
  else
  begin
    ExtractTemporaryFile('SimulcastUtilityWorkstationSetup.exe');
    PackagePath := ExpandConstant('{tmp}\SimulcastUtilityWorkstationSetup.exe');

    if not ShellExec('runas', PackagePath, '/SILENT /SUPPRESSMSGBOXES /NORESTART /SP-', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      FailureReason := 'Administrator approval is required to install Simulcast Utility for everyone on this workstation.';
      Exit;
    end;
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    FailureReason := Format('Simulcast Utility Setup failed with exit code %d.', [ResultCode]);
    Exit;
  end;

  Result := ImportReceiverConfiguration(FailureReason);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  FailureReason: String;
begin
  Result := '';
  WizardForm.StatusLabel.Caption := 'Installing Simulcast Utility...';

  if not InstallSelectedPackage(FailureReason) then
    Result := FailureReason;
end;
