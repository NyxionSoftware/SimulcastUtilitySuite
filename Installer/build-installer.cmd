@echo off
setlocal

set "ROOT=%~dp0.."
set "INSTALLER_ROOT=%~dp0"
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set "APPLICATION_PROJECT=%ROOT%\SimulcastUtility\SimulcastUtility.csproj"
set "PRODUCT_VERSION="

for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%INSTALLER_ROOT%Inno\GetProjectVersion.ps1" -ProjectPath "%APPLICATION_PROJECT%"`) do (
    set "PRODUCT_VERSION=%%V"
)

if not defined PRODUCT_VERSION (
    echo The product version could not be read from "%APPLICATION_PROJECT%".
    exit /b 1
)

set "ISCC_DEFINES=/DProductVersion=%PRODUCT_VERSION%"
echo Building Simulcast Utility %PRODUCT_VERSION%...

if not exist "%ISCC%" (
    echo Inno Setup 6 was not found at "%ISCC%".
    exit /b 1
)

if not exist "%INSTALLER_ROOT%Inno\SimulcastUtilitySetup.iss" (
    echo The installer source directory could not be verified.
    exit /b 1
)

echo Generating installer artwork from App-Icon.ico...
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%INSTALLER_ROOT%Inno\GenerateInstallerAssets.ps1" -IconPath "%ROOT%\SimulcastUtility\App-Icon.ico" -OutputDirectory "%INSTALLER_ROOT%Inno"
if errorlevel 1 exit /b %errorlevel%

echo Cleaning previous installer payloads...
if exist "%INSTALLER_ROOT%Payload\Application" rmdir /s /q "%INSTALLER_ROOT%Payload\Application"
if exist "%INSTALLER_ROOT%Output\SimulcastUtilitySetup.exe" del /q "%INSTALLER_ROOT%Output\SimulcastUtilitySetup.exe"
if exist "%INSTALLER_ROOT%Output\Packages\SimulcastUtilityUserSetup.exe" del /q "%INSTALLER_ROOT%Output\Packages\SimulcastUtilityUserSetup.exe"
if exist "%INSTALLER_ROOT%Output\Packages\SimulcastUtilityWorkstationSetup.exe" del /q "%INSTALLER_ROOT%Output\Packages\SimulcastUtilityWorkstationSetup.exe"

echo Publishing Simulcast Utility...
dotnet publish "%ROOT%\SimulcastUtility\SimulcastUtility.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -p:NuGetAudit=false -o "%INSTALLER_ROOT%Payload\Application"
if errorlevel 1 exit /b %errorlevel%

echo Building the per-user package...
"%ISCC%" /Qp %ISCC_DEFINES% "%INSTALLER_ROOT%Inno\SimulcastUtilityUser.iss"
if errorlevel 1 exit /b %errorlevel%

echo Building the workstation package...
"%ISCC%" /Qp %ISCC_DEFINES% "%INSTALLER_ROOT%Inno\SimulcastUtilityWorkstation.iss"
if errorlevel 1 exit /b %errorlevel%

echo Building the single Simulcast Utility installer...
"%ISCC%" /Qp %ISCC_DEFINES% "%INSTALLER_ROOT%Inno\SimulcastUtilitySetup.iss"
if errorlevel 1 exit /b %errorlevel%

echo.
echo Installer created at:
echo %INSTALLER_ROOT%Output\SimulcastUtilitySetup.exe

endlocal
