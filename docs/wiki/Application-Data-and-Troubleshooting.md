# Application Data and Troubleshooting

## Default data locations

Simulcast Utility stores user-specific data under:

```text
%LOCALAPPDATA%\SimulcastUtility
```

| Data | Location |
| --- | --- |
| Receivers | `receivers.json` |
| Logs | `Logs\` |
| Plugins | `Plugins\` |
| Plugin settings and data | `PluginData\` |

These paths can be overridden through application configuration.

## Receiver appears offline

- Verify its IPv4 address.
- Verify that the receiver ID matches the device at that address.
- Confirm that the receiver is reachable from the workstation.
- Refresh the receiver and inspect the error notification.

## Plugin does not load

- Confirm that the DLL contains a public, non-abstract `ISimulcastPlugin` implementation.
- Confirm that the plugin identifier is a permanent, non-empty, unique GUID.
- Do not include private copies of Simulcast Utility contract assemblies.
- Include third-party dependencies beside the plugin DLL or in its plugin folder.
- Review the latest log under `%LOCALAPPDATA%\SimulcastUtility\Logs`.

## Plugin cannot be deleted

The plugin likely retained an event handler, timer, window, static reference, media object, or injected interface element. Its `DisableAsync` implementation must release all active references and dispose owned resources.

## Uninstall data choices

The uninstaller preserves user data by default. It provides independent options to remove:

- Installed plugins and plugin data.
- Configured receivers.

Logs are preserved.
