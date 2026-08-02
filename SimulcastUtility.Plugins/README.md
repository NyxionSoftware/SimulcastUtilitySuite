# SimulcastUtility.Plugins

The plugins project defines the public extension contracts and manages the complete plugin lifecycle. It is the primary assembly referenced by third-party Simulcast Utility plugins.

## Responsibilities

- Define `ISimulcastPlugin`, `IPluginInfo`, and `IPluginContext`.
- Discover, import, initialize, enable, disable, refresh, unload, and remove plugins.
- Isolate plugin assemblies and dependencies through collectible load contexts.
- Dispatch application commands and provide receiver-management access.
- Expose plugin UI, dialog, notification, and theme services.
- Provide plugin-specific persistent storage through `IPluginDataStore`.
- Describe plugin settings and supported control types.
- Route application startup arguments to enabled plugins.

## Plugin settings

Plugins can expose text, numeric, toggle, checkbox, multi-checkbox, dropdown, and side-by-side-list settings through `IPluginSettingsProvider` and setting descriptors or attributes.

## Referencing this project

Plugin assemblies should reference `SimulcastUtility.Plugins` for compilation but must not copy the shared Simulcast Utility contract assemblies into their deployment directory. The host supplies those assemblies at runtime.

See the [Plugin Development Wiki](https://github.com/NyxionSoftware/SimulcastUtilitySuite/wiki/Plugin-Development) for examples, packaging guidance, settings, storage, UI integration, and themes.
