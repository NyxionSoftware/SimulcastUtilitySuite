# Plugin UI, Notifications, and Themes

## Notifications

```csharp
await context.UiManager.ShowNotificationAsync(
    pluginIdentifier,
    new PluginNotificationRequest("Example", "The action completed.", PluginNotificationSeverity.Success),
    cancellationToken);
```

Severities are `Information`, `Success`, and `Error`.

## Confirmations and dialogs

```csharp
bool confirmed = await context.UiManager.ShowConfirmationAsync(pluginIdentifier, "Confirm action", "Do you want to continue?", cancellationToken);
```

Use `ShowDialogAsync` for plugin-owned modeless content.

## Add interface elements

Find a named host element and add standard WPF controls:

```csharp
StackPanel? host = await context.UiManager.FindElementAsync<StackPanel>(pluginIdentifier, "NowPlayingOptionsHost", cancellationToken);

if (host is not null)
{
    CheckBox checkBox = new() { Content = "Enable example display" };
    host.Children.Add(checkBox);
}
```

Use application resources through `DynamicResource` or `Application.Current.TryFindResource`. Do not define replacement application control styles inside an injected view.

Register cleanup for every injected element:

```csharp
context.UiManager.RegisterCleanup(pluginIdentifier, () => host?.Children.Remove(checkBox));
```

Plugins may inspect named WPF elements and insert controls where appropriate, but they must tolerate missing or changed hosts and avoid relying on child indexes when a named element is available.

## Themes

Apply a plugin resource dictionary:

```csharp
Uri themeUri = new("/Plugin;component/Themes/ExampleTheme.xaml", UriKind.Relative);
await context.ThemeManager.ApplyResourceDictionaryAsync(pluginIdentifier, themeUri, cancellationToken);
await context.ThemeManager.SetWindowChromeModeAsync(pluginIdentifier, PluginWindowChromeMode.Dark, cancellationToken);
```

Remove it during disablement:

```csharp
await context.ThemeManager.RemoveResourceDictionaryAsync(pluginIdentifier, cancellationToken);
```

Theme dictionaries should override existing application color keys. Controls that consume the corresponding dynamic brushes update throughout the application.
