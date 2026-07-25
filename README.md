# Simulcast Utility Suite

<p align="center">
  <img width="220" height="220" alt="SimulcastUtility-removebg-preview" src="https://github.com/user-attachments/assets/1d93aba8-0d6f-4263-aa51-eeb5a979f891" />
</p>

<p align="center">
    <strong>A modern, asynchronous WPF application for discovering, monitoring, configuring, and controlling RTN simulcast receivers.</strong>
</p>

<p align="center">
    <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white"></a>
    <a href="https://learn.microsoft.com/dotnet/desktop/wpf/"><img src="https://img.shields.io/badge/WPF-Windows-0078D6?style=flat"></a>
    <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/blob/main/LICENSE"><img src="https://img.shields.io/github/license/NyxionSoftware/SimulcastUtilitySuite?style=flat"></a>
    <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/stargazers"><img src="https://img.shields.io/github/stars/NyxionSoftware/SimulcastUtilitySuite?style=flat"></a>
    <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/issues"><img src="https://img.shields.io/github/issues/NyxionSoftware/SimulcastUtilitySuite?style=flat"></a>
    <a href="https://github.com/NyxionSoftware/SimulcastUtilitySuite/releases"><img src="https://img.shields.io/github/v/release/NyxionSoftware/SimulcastUtilitySuite?style=flat"></a>
</p>

---

> [!WARNING]
> **Work in Progress**
>
> Simulcast Utility Suite is under active development. Features, Extensionability, the user interface, and especially the plugin framework may evolve between releases as the project continues to grow.

---

# 📖 Overview

**Simulcast Utility Suite** is a modern Windows desktop application built with **WPF** for discovering, monitoring, configuring, and controlling **RTN** simulcast receivers.

**RTN (Racetrack Television Network)** is a horse racing television network that delivers live racing and simulcast content to racetracks, casinos, sportsbooks, and off-track betting facilities throughout North America.

Traditionally, RTN receivers are managed using **RCN Recon**, the official management application developed by RTN. Simulcast Utility Suite was created as a modern alternative that focuses on performance, responsiveness, and extensibility while providing the same core receiver management capabilities.

Unlike traditional management applications, Simulcast Utility Suite is built around a fully asynchronous architecture. Commands are executed without blocking the user interface, receiver communication occurs in the background, and operations can run simultaneously without forcing the user to wait for one task to finish before starting another.

The long-term goal of this project is to become a powerful platform for RTN receiver management while providing a flexible plugin framework that allows additional functionality to be added without modifying the core application.

If you'd like to try the latest version, simply download it from the **Releases** section of this repository.

---

# 📥 Download

The latest compiled version is always available from the GitHub Releases page.

<p align="center">

## ➜ https://github.com/NyxionSoftware/SimulcastUtilitySuite/releases/latest

</p>

No development tools are required to run the application.

---

# 📷 Screenshots

## Main Dashboard

<p align="center">
<img width="1186" height="808" alt="image" src="https://github.com/user-attachments/assets/433ff3c1-b3a5-4324-9b83-1a86a78ad21a" />
</p>

---

## Receiver Management

<p align="center">
<img width="966" height="698" alt="image" src="https://github.com/user-attachments/assets/4e04b7b6-6778-4a75-8592-baaa0c613366" />
</p>

---

## Plugin Example

<p align="center">
<img width="1591" height="946" alt="image" src="https://github.com/user-attachments/assets/b36151e5-c5cf-4b55-bfbb-043abcfff4d3" />
</p>

---

# ✨ Features

- ⚡ Fully asynchronous receiver communication
- 📡 Live receiver monitoring
- 📺 Current EPG retrieval
- 🎛 Receiver channel control
- ⚙ Receiver configuration management
- 🧩 Extensible .NET 10 plugin framework
- 🎨 Modern WPF dark-themed interface
- 💾 Persistent receiver configuration
- 🧵 Thread-safe receiver updates
- 🚀 Built with performance and responsiveness in mind

---

# 🔌 Plugin Framework

One of the primary goals of Simulcast Utility Suite is extensibility.

Plugins are written as **.NET 10 WPF Class Libraries** and are loaded automatically when the application starts.

### Plugins can currently

- 📋 Easily retrieve the application's receiver collection
- 📡 Easily send commands to receivers through the built-in controller service
- 🖥 Easily modify the Main Window by adding, removing, or modifying controls
- ⚙ Access application configuration services
- 📢 Subscribe to application events including:
  - Selected Receiver Changed
  - Receiver Added
  - Receiver Removed
  - Receiver Updated
  - Receiver Status Changed
  - Receiver Refreshed
  - Receiver Configuration Changed

> Additional events and services will continue to be added as the framework evolves.

### Plugin Folder Layout

Plugins may either be placed directly inside the **Plugins** directory or inside their own subdirectory when additional dependencies are required.

```
Plugins/
│
├── ExamplePlugin.dll
│
├── RTNSchedulePlugin/
│   ├── RTNSchedulePlugin.dll
│   ├── HtmlAgilityPack.dll
│   └── ...
│
└── AnotherPlugin/
    ├── AnotherPlugin.dll
    └── ...
```

The application automatically searches the Plugins directory recursively and loads all compatible plugins.

---

# 🖥 Main Dashboard

The dashboard provides quick access to important receiver information including:

- Receiver Status
- Receiver Software Version
- Current Channel
- Current Event Information
- Program Duration
- Receiver Details
- Plugin-Provided Dashboard Cards

---

# 🚀 Getting Started

## Requirements

- Windows 10 or Windows 11
- .NET 10 Runtime

Clone the repository if you wish to build or contribute to the project.

```bash
git clone https://github.com/NyxionSoftware/SimulcastUtilitySuite.git
```

---

# 🧪 Plugin Development

The recommended approach is to develop plugins outside of this repository while referencing the plugin abstraction project.

Example development layout:

```
Development/
│
├── SimulcastUtilitySuite/
│
└── MyPlugin/
```

Reference **SimulcastUtility.Plugin.Abstractions** from your plugin project.

For the best development experience:

1. Open both projects within the same Visual Studio solution.
2. Configure your plugin project to copy its compiled output into the application's **Plugins** folder after every build.
3. Start debugging normally—the application will automatically discover and load your updated plugin.

---

# ⚙ Configuration

Receiver configuration is automatically stored locally.

Default configuration location:

```
%LOCALAPPDATA%\SimulcastUtilitySuite\
```

---

# 🛣 Roadmap

- [x] Receiver Manual Discovery
- [x] Receiver Channel Control
- [x] Receiver Configuration
- [x] Crude Plugin Framework
- [x] EPG Retrieval *(RTN's current EPG commands tend to be unpredictable.)*
- [ ] Receiver Automatic Discovery
- [ ] Live Modal Notifications
- [ ] Receiver Group Management
- [ ] Theme Customization
- [ ] Multi-language Support
- [ ] Server-mode Statistic Support

---

# 🤝 Contributing

Contributions, bug reports, feature requests, and plugin examples are always welcome.

If you'd like to contribute:

1. Fork the repository.
2. Create a feature branch.
3. Commit your changes.
4. Open a Pull Request.

---

# 📄 License

This project is licensed under the MIT License.

See the **LICENSE** file for additional information.

---

<p align="center">
Made with ❤️ by <strong>Nyxion Software</strong>
</p>
