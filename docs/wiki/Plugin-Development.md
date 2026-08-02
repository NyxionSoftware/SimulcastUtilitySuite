# Plugin Development Overview

A plugin is a .NET class library containing at least one public implementation of `ISimulcastPlugin`.

## Core lifecycle

```text
InitializeAsync → EnableAsync → HandleApplicationArgumentsAsync
                              ↓
                         DisableAsync
```

- `InitializeAsync` receives and stores the `IPluginContext`.
- `EnableAsync` starts plugin services and attaches interface elements or event handlers.
- `HandleApplicationArgumentsAsync` processes startup arguments belonging to the plugin.
- `DisableAsync` reverses everything performed during enablement.

## Plugin context

`IPluginContext` provides:

| Member | Purpose |
| --- | --- |
| `InstallationDirectory` | Main application installation directory |
| `ReceiverRepository` | Persistent receiver storage |
| `ReceiverManager` | Receiver collection, selection, and configuration |
| `ReceiverCommandManager` | Receiver commands and status events |
| `ApplicationDispatcher` | Commands directed at the main application |
| `ThemeManager` | Application resource dictionaries and title-bar mode |
| `UiManager` | UI lookup, dialogs, notifications, and cleanup registration |
| `DataStore` | Plugin-specific persistent JSON data |

## Important lifecycle rule

Every subscription, timer, window, resource dictionary, media object, and injected control created by a plugin must be detached or disposed when the plugin is disabled. Runtime unloading cannot complete while the application still holds references into the plugin assembly.
