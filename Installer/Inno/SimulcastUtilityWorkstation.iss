#define ProductName "Simulcast Utility"
#define PublisherName "NyxionSoftware"
#define ProjectRoot "..\.."
#include "Version.iss"

[Setup]
AppId={{80070FC8-6963-4D05-B410-0D87494952CB}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppPublisher={#PublisherName}
DefaultDirName={commonpf64}\NyxionSoftware\Simulcast Utility
DefaultGroupName={#ProductName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\Output\Packages
OutputBaseFilename=SimulcastUtilityWorkstationSetup
SetupIconFile={#ProjectRoot}\SimulcastUtility\App-Icon.ico
WizardSmallImageFile=App-Icon.bmp
WizardSmallImageBackColor=$251D19
WizardImageFile=WizardImage.bmp
WizardImageBackColor=$171210
UninstallDisplayIcon={app}\SimulcastUtility.exe
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#ProductVersion}
VersionInfoCompany={#PublisherName}
VersionInfoDescription={#ProductName} Workstation Installer
VersionInfoProductName={#ProductName}
VersionInfoProductVersion={#ProductVersion}

[Files]
Source: "..\Payload\Application\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{commonprograms}\Simulcast Utility"; Filename: "{app}\SimulcastUtility.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SimulcastUtility.exe"
Name: "{commondesktop}\Simulcast Utility"; Filename: "{app}\SimulcastUtility.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SimulcastUtility.exe"

[Code]
#include "SimulcastTheme.iss"

procedure InitializeWizard;
begin
  ApplySimulcastTheme;
end;
