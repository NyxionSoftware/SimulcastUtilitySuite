<p align="center">
  <img width="1774" height="887" alt="Simulcast Utility receiver control artwork" src="https://github.com/user-attachments/assets/ee1a0e1c-6808-43b0-8f2f-7ebc03940512" />
</p>

<h1 align="center">Simulcast Utility Suite</h1>

<p align="center">
  <strong>A modern, asynchronous Windows application for discovering, monitoring, configuring, and controlling RTN simulcast receivers.</strong>
</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white" /></a>
  <a href="https://learn.microsoft.com/dotnet/desktop/wpf/"><img alt="WPF for Windows" src="https://img.shields.io/badge/WPF-Windows-0078D6?style=flat" /></a>
  <a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/github/license/NyxionSoftware/SimulcastUtilitySuite" /></a>
  <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/stargazers"><img alt="GitHub stars" src="https://img.shields.io/github/stars/NyxionSoftware/SimulcastUtilitySuite?style=flat" /></a>
  <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/issues"><img alt="GitHub issues" src="https://img.shields.io/github/issues/NyxionSoftware/SimulcastUtilitySuite?style=flat" /></a>
  <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/releases"><img alt="Latest release" src="https://img.shields.io/github/v/release/NyxionSoftware/SimulcastUtilitySuite?include_prereleases" /></a>
</p>

---

> [!WARNING]
> **Work in progress**
>
> Simulcast Utility Suite is under active development and has not reached a stable public release. Features, the interface, and especially the plugin framework may evolve as the project grows.

---

# 📖 Overview

**Simulcast Utility Suite** is a modern WPF desktop application for discovering, monitoring, configuring, and controlling **RTN** simulcast receivers from one responsive interface.

**RTN (Racetrack Television Network)** delivers live racing and simulcast content to racetracks, casinos, sportsbooks, and off-track betting facilities throughout North America. Simulcast Utility Suite provides a modern receiver-management experience focused on responsiveness, clear live status, and extensibility.

Receiver communication and long-running operations use an asynchronous architecture so the interface remains responsive. A central receiver manager acts as the source of truth for selection, configuration, connection state, activity, channel information, and plugin integrations.

The application has grown beyond basic receiver control into an extensible platform. Plugins can add interface elements, commands, schedules, media feeds, themes, settings, persistent data, and application behavior without modifying the core application.

---

# 📥 Download

Once public builds are available, the latest installer will be published on the GitHub Releases page.

<p align="center">
  <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/releases/latest"><strong>➜ Download the latest release</strong></a>
</p>

The self-contained Windows x64 installer does not require users to install the .NET runtime separately. Setup supports both per-user and workstation-wide installations.

---

# 📷 Application Showcase

## Main Dashboard

Monitor receiver status, activity, channel information, EPG progress, and receiver details while keeping channel controls and actions close at hand.

<img width="1186" height="808" alt="Main Dashboard Page" src="https://github.com/user-attachments/assets/fefd9e8d-64f7-47be-a39e-5559dc4e4e4e" />

---

## Receiver Management

Discover, validate, add, edit, delete, and fluidly reorder receivers without leaving the main application shell.

<img width="1186" height="808" alt="Receiver Management Page" src="https://github.com/user-attachments/assets/db389a7f-fac2-4fea-8fcf-f95ba519f689" />

---

## Plugin Management

Import, enable, disable, refresh, configure, and actively unload plugins at runtime. Plugin-defined settings are presented using controls that match the application theme.

<img width="1186" height="808" alt="Plugin Management Page" src="https://github.com/user-attachments/assets/1ccc5f1e-aaae-4e8c-aa84-6b6a7494443d" />

---

## Virtual Remote

Open one independent, non-modal virtual remote per receiver and continue working in the main window while commands are sent.

<img width="386" height="723" alt="Virtual Remote Window" src="https://github.com/user-attachments/assets/82f44fa5-e31c-48ee-86a0-c9b812089d18" />

---

# ✨ Features

- ⚡ Asynchronous receiver discovery, communication, and refresh operations
- 📡 Live online, offline, warning, editing, transmitting, and idle activity states
- 📺 Current channel, EPG information, event times, and live progress display
- 🎛 Validated channel control with command throttling and visual feedback
- 🎲 Plugin-powered channel discovery actions such as **Feeling Lucky**
- ⚙ Add, edit, delete, validate, and fluidly reorder receiver configurations
- 🕹 Independent virtual remote windows with live activity feedback
- 🔔 Animated, stacked success, information, and error notifications
- 🔄 Immediate startup refresh and guarded per-receiver or refresh-all actions
- 🧩 Runtime plugin importing, enabling, disabling, refreshing, and removal
- 🎨 Plugin-provided application themes, including title-bar appearance
- 💾 Persistent receiver configuration and plugin-specific data storage
- 📦 Styled per-user and workstation-wide installers with optional receiver import

---

# 🖥 Main Dashboard

The dashboard provides quick access to the selected receiver and its controls:

- Receiver name, identifier, IP address, MAC address, and software version
- Online status, last-seen time, and current receiver activity
- Current channel, event information, start and end times, and progress
- Validated channel entry and plugin-provided channel actions
- Refresh, edit, virtual remote, and other receiver actions
- Plugin-provided cards, controls, schedules, and media experiences

Receiver selection is synchronized through the receiver manager, ensuring every view and open virtual remote reflects the current receiver information.

---

# 🔌 Plugin Framework

Plugins implement `ISimulcastPlugin` from `SimulcastUtility.Plugins` and receive an `IPluginContext` during initialization. Plugins can be imported as a single DLL, a collection of DLLs, or a ZIP package.

## Plugins can currently

- 📋 Access and observe the receiver collection and selected receiver
- 📡 Dispatch receiver commands through the application's command services
- 🖥 Locate and extend application views with custom WPF elements
- 💬 Open application dialogs and publish themed notifications
- 🎨 Register themes and control light or dark title-bar appearance
- 💾 Store plugin-specific data through the application data-store API
- ⚙ Expose named settings, descriptions, choices, toggles, and list selectors
- 🧭 Handle application startup arguments
- 🔔 Subscribe to receiver and application events
- ♻ Enable, disable, refresh, unload, and remove plugin code at runtime

## Plugin settings controls

The built-in settings experience currently supports:

- Text
- Numeric
- Toggle
- Checkbox
- MultiCheckbox
- Dropdown (choice)
- SideBySideList

<img width="620" height="720" alt="Examples of Configuration Settings" src="https://github.com/user-attachments/assets/9a19339c-8154-4b82-b08e-b14c7825060b" />


## Plugin folder layout

Plugins may be placed directly inside the configured plugin directory or in their own subdirectory when dependencies are required.

```text
Plugins/
├── ExamplePlugin.dll
├── RTNSPlugin/
│   ├── RTNPlugin.dll
│   └── PluginDependency.dll
└── ExamplePlugin2/
    ├── Plugin.dll
    └── MediaDependency.dll
```

The application recursively discovers compatible plugins. Shared Simulcast Utility contract assemblies should not be copied into an individual plugin directory.

For complete guidance, see the [Plugin Development Wiki](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Plugin-Development).

---

# 🚀 Getting Started

## End-user requirements

- Windows 10 version 1809 or newer
- Windows 11
- 64-bit Windows installation

The official installer publishes a self-contained application, so no separate .NET installation is required.

## Build requirements

- .NET 10 SDK
- Visual Studio 2026 or another IDE with WPF support
- Inno Setup 6 when building the installer

Clone and build the project:

```powershell
git clone https://github.com/NyxionSoftware/SimulcastUtilitySuite.git
cd SimulcastUtilitySuite
dotnet restore
dotnet build SimulcastUtilitySuite.slnx
dotnet run --project SimulcastUtility\SimulcastUtility.csproj
```

---

# 📦 Building the Installer

Run the installer builder from the repository root:

```bat
Installer\build-installer.cmd
```

An optional version can be supplied as the first argument:

```bat
Installer\build-installer.cmd 1.0.0
```

The generated installer is written to `Installer\Output\SimulcastUtilitySetup.exe` and lets the user choose between:

- A per-user installation under `%LOCALAPPDATA%\Programs` without elevation
- A workstation installation under `%ProgramFiles%\NyxionSoftware` with administrator approval

The installer can optionally import an existing `receivers.json`. The themed uninstaller independently offers removal of receiver configuration, installed plugins, and plugin data.

---

# ⚙ Configuration and Application Data

Unless overridden through configuration, application data is stored under:

```text
%LOCALAPPDATA%\SimulcastUtility
```

| Data | Default location |
| --- | --- |
| Receiver configuration | `receivers.json` |
| Logs | `Logs\` |
| Installed plugins | `Plugins\` |
| Plugin data and settings | `PluginData\` |

Logging and plugin directories can be changed through application configuration. The uninstaller preserves application data unless the user explicitly selects a cleanup option.

---

# 🧱 Solution Structure

| Project | Responsibility |
| --- | --- |
| `SimulcastUtility` | WPF executable, startup, and application windows |
| `SimulcastUtility.Wpf` | Views, view models, controls, themes, and WPF plugin services |
| `SimulcastUtility.Application` | Receiver workflows, commands, events, and application interfaces |
| `SimulcastUtility.Core` | Receiver models, enums, exceptions, and shared domain logic |
| `SimulcastUtility.Infrastructure` | Receiver persistence and infrastructure services |
| `SimulcastUtility.Configuration` | Application configuration models |
| `SimulcastUtility.Logging` | Logging infrastructure |
| `SimulcastUtility.Plugins` | Plugin contracts, loading, settings, storage, and runtime management |
| `Installer` | Inno Setup source, generated payload, and installer builder |

---

# 🛣 Roadmap

- [x] Manual receiver discovery and validation
- [x] Receiver channel control and virtual remote
- [x] Receiver configuration, deletion, and drag-to-reorder
- [x] Live receiver status, activity, EPG, and progress
- [x] Animated application notifications
- [x] Runtime plugin management and active unloading
- [x] Plugin settings and persistent data-store APIs
- [x] Application-wide plugin themes
- [x] Styled per-user and workstation installer
- [ ] Automatic receiver discovery
- [ ] Receiver group management
- [ ] Multi-language support
- [ ] Server-mode statistics and monitoring

---

# 📚 Documentation

The [GitHub Wiki](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki) covers application usage, receiver management, configuration, plugin installation, plugin development, settings, data storage, UI integration, and themes.

- [Getting Started](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Getting-Started)
- [Managing Receivers](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Managing-Receivers)
- [Managing Plugins](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Managing-Plugins)
- [Creating Your First Plugin](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Creating-Your-First-Plugin)
- [Building and Distributing Plugins](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Building-and-Distributing-Plugins)
- [Plugin Settings and Data](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Plugin-Settings-and-Data)
- [Plugin UI, Notifications, and Themes](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Plugin-UI-Notifications-and-Themes)
- [Virtual Remote](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Virtual-Remote)
- [Application Data and Troubleshooting](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Application-Data-and-Troubleshooting)

---

# 🤝 Contributing

Contributions, bug reports, feature requests, documentation improvements, and example plugins are welcome.

1. Fork the repository.
2. Create a focused feature branch.
3. Commit and test your changes.
4. Open a pull request describing the change.

The `main` branch contains the current application. The `legacy` branch preserves the previous implementation.

---

# 📄 License

Simulcast Utility Suite is available under the [MIT License](LICENSE).

---

<p align="center">
  Made with 💜 by <a href="https://nyxionsoftware.com/"><strong>Nyxion Software</strong></a>
</p>
