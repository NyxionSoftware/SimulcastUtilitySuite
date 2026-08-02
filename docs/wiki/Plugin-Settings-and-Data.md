# Plugin Settings and Data

## Data store

Each plugin receives an isolated `IPluginDataStore`. Use it instead of writing arbitrary files into the application directory.

```csharp
await context.DataStore.WriteAsync("cache", cache, cancellationToken);
CacheModel? cache = await context.DataStore.ReadAsync<CacheModel>("cache", cancellationToken);
await context.DataStore.DeleteAsync("cache", cancellationToken);
bool exists = context.DataStore.Exists("cache");
```

Data is stored beneath the plugin-data directory using the plugin identifier.

## Expose settings

Implement `IPluginSettingsProvider` and return a settings object whose properties use `PluginSettingAttribute`.

```csharp
public sealed class ExampleSettings
{
    [PluginSetting("Display name", "Name shown by the plugin.", PluginSettingControlType.Text, Group = "General", Order = 1)]
    public string DisplayName { get; set; } = "Example";

    [PluginSetting("Enabled feature", "Turns the example feature on or off.", PluginSettingControlType.Toggle, Group = "General", Order = 2)]
    public bool EnabledFeature { get; set; } = true;
}
```

Supported controls:

- `Text`
- `Numeric`
- `Toggle`
- `Checkbox`
- `MultiCheckbox`
- `Dropdown`
- `SideBySideList`

Return dropdown and list choices from `GetSettingOptions`. React to saved changes in `OnSettingChangedAsync`.

```csharp
public IReadOnlyList<PluginSettingOption> GetSettingOptions(string settingKey)
{
    return settingKey == nameof(ExampleSettings.Mode)
        ? new[] { new PluginSettingOption("Off", "Off"), new PluginSettingOption("Active", "Active") }
        : Array.Empty<PluginSettingOption>();
}
```

The plugin manager automatically persists attributed setting properties through the plugin data store.
