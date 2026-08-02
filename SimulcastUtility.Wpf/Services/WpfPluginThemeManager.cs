using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;
using System.Windows;
using System.Windows.Media;

namespace SimulcastUtility.Wpf.Services
{
    public sealed class WpfPluginThemeManager : IPluginThemeManager
    {
        private readonly Dictionary<Guid, PluginThemeResources> _pluginResources = new();
        private readonly Dictionary<Guid, PluginWindowChromeMode> _windowChromeModes = new();

        public async Task ApplyResourceDictionaryAsync(Guid pluginIdentifier, Uri resourceDictionaryUri, CancellationToken cancellationToken = default)
        {
            if (pluginIdentifier == Guid.Empty)
                throw new ArgumentException("A plugin identifier is required.", nameof(pluginIdentifier));

            ArgumentNullException.ThrowIfNull(resourceDictionaryUri);
            cancellationToken.ThrowIfCancellationRequested();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RemoveResourceDictionary(pluginIdentifier);
                ResourceDictionary resourceDictionary = new() { Source = resourceDictionaryUri };
                ResourceDictionary applicationResources = System.Windows.Application.Current.Resources;
                applicationResources.MergedDictionaries.Add(resourceDictionary);
                Dictionary<object, PreviousResource> previousResources = new();

                foreach (KeyValuePair<object, object> resource in EnumerateResources(resourceDictionary))
                {
                    if (resource.Value is not Color color)
                        continue;

                    ApplyResourceOverride(applicationResources, previousResources, resource.Key, color);

                    if (resource.Key is string colorKey && colorKey.EndsWith("Color", StringComparison.Ordinal))
                        ApplyResourceOverride(applicationResources, previousResources, $"{colorKey[..^5]}Brush", new SolidColorBrush(color));
                }

                _pluginResources[pluginIdentifier] = new PluginThemeResources(resourceDictionary, previousResources);
            });
        }

        public async Task RemoveResourceDictionaryAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => RemoveResourceDictionary(pluginIdentifier));
        }

        public async Task SetWindowChromeModeAsync(Guid pluginIdentifier, PluginWindowChromeMode mode, CancellationToken cancellationToken = default)
        {
            if (pluginIdentifier == Guid.Empty)
                throw new ArgumentException("A plugin identifier is required.", nameof(pluginIdentifier));

            cancellationToken.ThrowIfCancellationRequested();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _windowChromeModes[pluginIdentifier] = mode;
                WindowChromeService.SetMode(mode);
            });
        }

        private void RemoveResourceDictionary(Guid pluginIdentifier)
        {
            _windowChromeModes.Remove(pluginIdentifier);
            WindowChromeService.SetMode(_windowChromeModes.Count == 0 ? PluginWindowChromeMode.Dark : _windowChromeModes.Last().Value);

            if (!_pluginResources.Remove(pluginIdentifier, out PluginThemeResources? pluginResources))
                return;

            ResourceDictionary applicationResources = System.Windows.Application.Current.Resources;

            foreach (KeyValuePair<object, PreviousResource> resource in pluginResources.PreviousResources)
            {
                if (resource.Value.HadLocalValue)
                    applicationResources[resource.Key] = resource.Value.Value;
                else
                    applicationResources.Remove(resource.Key);
            }

            applicationResources.MergedDictionaries.Remove(pluginResources.Dictionary);
        }

        private static IEnumerable<KeyValuePair<object, object>> EnumerateResources(ResourceDictionary resourceDictionary)
        {
            foreach (ResourceDictionary mergedDictionary in resourceDictionary.MergedDictionaries)
            {
                foreach (KeyValuePair<object, object> resource in EnumerateResources(mergedDictionary))
                    yield return resource;
            }

            foreach (object key in resourceDictionary.Keys)
                yield return new KeyValuePair<object, object>(key, resourceDictionary[key]);
        }

        private static void ApplyResourceOverride(ResourceDictionary applicationResources, IDictionary<object, PreviousResource> previousResources, object key, object value)
        {
            if (!previousResources.ContainsKey(key))
            {
                bool hadLocalValue = applicationResources.Keys.Cast<object>().Contains(key);
                previousResources[key] = new PreviousResource(hadLocalValue, hadLocalValue ? applicationResources[key] : null);
            }

            applicationResources[key] = value;
        }

        private sealed record PreviousResource(bool HadLocalValue, object? Value);

        private sealed record PluginThemeResources(ResourceDictionary Dictionary, IReadOnlyDictionary<object, PreviousResource> PreviousResources);
    }
}
