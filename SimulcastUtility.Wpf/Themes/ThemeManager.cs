using SimulcastUtility.Wpf.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace SimulcastUtility.Wpf.Themes
{
    public sealed class ThemeManager : IThemeManager
    {
        private readonly List<ThemeDefinition> _themes = new();
        private readonly Uri _defaultThemeUri;
        private ResourceDictionary? _activeColorDictionary;

        public IReadOnlyList<ThemeDefinition> Themes => _themes;

        public ThemeDefinition CurrentTheme { get; private set; }

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public ThemeManager()
        {
            _defaultThemeUri = new Uri("/SimulcastUtility.Wpf;component/Themes/Default/Colors.xaml", UriKind.Relative);

            CurrentTheme = new ThemeDefinition("default", "Default", _defaultThemeUri);

            _themes.Add(CurrentTheme);
        }

        public void RegisterTheme(ThemeDefinition theme)
        {
            ArgumentNullException.ThrowIfNull(theme);

            if (_themes.Any(existing => string.Equals(existing.Id, theme.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A theme with the ID '{theme.Id}' is already registered.");

            _themes.Add(theme);
        }

        public bool UnregisterTheme(string themeId)
        {
            ThemeDefinition? theme = _themes.FirstOrDefault(existing => string.Equals(existing.Id, themeId, StringComparison.OrdinalIgnoreCase));

            if (theme is null || theme.Id == "default")
                return false;

            if (CurrentTheme.Id == theme.Id)
                ApplyDefaultTheme();

            return _themes.Remove(theme);
        }

        public void ApplyTheme(string themeId)
        {
            ThemeDefinition theme = _themes.FirstOrDefault(existing => string.Equals(existing.Id, themeId, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Theme '{themeId}' is not registered.");

            ApplyTheme(theme);
        }

        public void ApplyDefaultTheme()
        {
            ApplyTheme("default");
        }

        private void ApplyTheme(ThemeDefinition theme)
        {
            System.Windows.Application application = System.Windows.Application.Current ?? throw new InvalidOperationException("The WPF application is not running.");

            ResourceDictionary newDictionary = new()
            {
                Source = theme.ResourceUri
            };

            Collection<ResourceDictionary> dictionaries = application.Resources.MergedDictionaries;

            int insertionIndex = 0;

            if (_activeColorDictionary is not null)
            {
                int existingIndex = dictionaries.IndexOf(_activeColorDictionary);

                if (existingIndex >= 0)
                {
                    insertionIndex = existingIndex;
                    dictionaries.RemoveAt(existingIndex);
                }
            }

            dictionaries.Insert(insertionIndex, newDictionary);

            ThemeDefinition previousTheme = CurrentTheme;

            _activeColorDictionary = newDictionary;
            CurrentTheme = theme;

            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(previousTheme, theme));
        }
    }
}
