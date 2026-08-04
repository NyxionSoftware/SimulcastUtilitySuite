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
                Dictionary<object, PreviousResource> previousResources = new();
                List<KeyValuePair<object, object>> resourceOverrides = new();

                foreach (KeyValuePair<object, object> resource in EnumerateResources(resourceDictionary))
                {
                    if (resource.Value is not Color color)
                        continue;

                    resourceOverrides.Add(new KeyValuePair<object, object>(resource.Key, color));

                    if (resource.Key is string colorKey && colorKey.EndsWith("Color", StringComparison.Ordinal))
                        resourceOverrides.Add(new KeyValuePair<object, object>($"{colorKey[..^5]}Brush", new SolidColorBrush(color)));
                }

                foreach (KeyValuePair<object, object> resourceOverride in resourceOverrides)
                    CapturePreviousResource(previousResources, resourceOverride.Key);

                foreach (KeyValuePair<object, object> resourceOverride in resourceOverrides)
                    applicationResources[resourceOverride.Key] = resourceOverride.Value;

                applicationResources.MergedDictionaries.Add(resourceDictionary);

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
            applicationResources.MergedDictionaries.Remove(pluginResources.Dictionary);

            foreach (KeyValuePair<object, PreviousResource> resource in pluginResources.PreviousResources)
            {
                if (resource.Value.HadValue)
                    applicationResources[resource.Key] = resource.Value.Value;
                else
                    applicationResources.Remove(resource.Key);
            }
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

        private static void CapturePreviousResource(IDictionary<object, PreviousResource> previousResources, object key)
        {
            if (previousResources.ContainsKey(key))
                return;

            object? previousValue = System.Windows.Application.Current.TryFindResource(key);

            if (previousValue is Freezable freezable)
                previousValue = freezable.CloneCurrentValue();

            previousResources[key] = new PreviousResource(previousValue is not null, previousValue);
        }

        private sealed record PreviousResource(bool HadValue, object? Value);

        private sealed record PluginThemeResources(ResourceDictionary Dictionary, IReadOnlyDictionary<object, PreviousResource> PreviousResources);
    }
}
