# Managing Plugins

Open **Manage Plugins** from the main toolbar.

## Import formats

Simulcast Utility accepts:

- One plugin DLL.
- A group of related DLL files.
- A ZIP archive containing a plugin and its dependencies.

Single DLL plugins are copied into the plugin directory. Collections and ZIP archives are placed in their own plugin subdirectory.

## Enable and disable

Use the plugin's toggle to enable or disable it at runtime. A disabled plugin must detach its event handlers, interface elements, dialogs, themes, and other active references.

## Refresh

Select **Refresh Installed Plugins** after changing files in the plugin directory. Refresh discovers newly added plugins and reloads available plugin information.

## Settings

The Settings button appears only when a plugin implements `IPluginSettingsProvider`. Settings are saved through the plugin data-store API and applied through the plugin's change callback.

## Delete

Deleting a plugin asks for confirmation, disables and unloads the plugin, releases its load context, and removes its files. A plugin that retains active references may prevent its assembly from unloading.

## Plugin directory

Use **Open Plugin Directory** from Manage Plugins, or navigate to:

```text
%LOCALAPPDATA%\SimulcastUtility\Plugins
```
