# Simulcast Utility Installer

`SimulcastUtilitySetup.exe` is the only file distributed to end users. It presents one themed wizard and installs Simulcast Utility either for the current user or for the entire workstation.

## Build

From the repository root:

```bat
Installer\build-installer.cmd
```

The finished installer is written to `Installer\Output\SimulcastUtilitySetup.exe`.

The application and installer use `SimulcastUtility\App-Icon.ico` as the single icon source. The build command regenerates Inno Setup's required BMP artwork from that ICO before compiling.

## Installation scope

The setup wizard manually asks whether the application should be installed only for the current user or for everyone on the workstation. The per-user package installs under `%LOCALAPPDATA%\Programs` without elevation. The workstation package requests administrator approval and installs under `%ProgramFiles%\NyxionSoftware`.

Setup can optionally import a validated `receivers.json` file into the current Windows account. If `receivers.json` is beside the installer, it is selected automatically.

Both uninstallers use the Simulcast theme and ask whether installed plugins/plugin data and configured receivers should also be removed. These cleanup options are unchecked by default; logs and unrelated application data are preserved.

Imported plugins, receiver configuration, logs, and plugin data remain under the user's local application-data directory and are not removed by uninstalling the application.

To build a specific version, pass it as the first argument:

```bat
Installer\build-installer.cmd 1.2.3
```

When omitted, the version in `Installer\Inno\Version.iss` is used.

Inno Setup 6 must be installed. The build script detects its standard 32-bit or 64-bit installation directory.
