# Getting Started

## Installation

Run `SimulcastUtilitySetup.exe` and select an installation scope:

- **Only for me** installs under `%LOCALAPPDATA%\Programs` and does not require administrator approval.
- **Everyone on this workstation** installs under `%ProgramFiles%\NyxionSoftware` and requires administrator approval.

Setup can optionally import an existing `receivers.json`. Leave the field blank to start with no configured receivers.

## First launch

When the application opens, it refreshes all configured receivers. The receiver list shows each receiver's connection status, and selecting a receiver updates the workspace with its current information.

If no receivers are configured, select **Manage Receivers** and add one through discovery.

## Main screen

The main screen contains:

- A searchable receiver list.
- Receiver identity, version, network, connection, and activity information.
- Current channel and program information with live progress.
- Channel controls.
- Receiver actions for refreshing or editing the selected receiver.
- Access to the virtual remote, receiver manager, and plugin manager.

Notifications appear in the lower-right corner. Success, information, and error notifications stack independently and close automatically after the configured duration.
