#define ProductName "Simulcast Utility"
#define PublisherName "NyxionSoftware"
#define ProjectRoot "..\.."
#include "Version.iss"

[Setup]
AppId={{D197BB08-090B-4A6B-B993-313E541A87F9}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppPublisher={#PublisherName}
DefaultDirName={localappdata}\Programs\Simulcast Utility
DefaultGroupName={#ProductName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\Output\Packages
OutputBaseFilename=SimulcastUtilityUserSetup
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
VersionInfoDescription={#ProductName} Per-User Installer
VersionInfoProductName={#ProductName}
VersionInfoProductVersion={#ProductVersion}

[Files]
Source: "..\Payload\Application\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Simulcast Utility"; Filename: "{app}\SimulcastUtility.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SimulcastUtility.exe"
Name: "{userdesktop}\Simulcast Utility"; Filename: "{app}\SimulcastUtility.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SimulcastUtility.exe"

[Code]
#include "SimulcastTheme.iss"

procedure InitializeWizard;
begin
  ApplySimulcastTheme;
end;
