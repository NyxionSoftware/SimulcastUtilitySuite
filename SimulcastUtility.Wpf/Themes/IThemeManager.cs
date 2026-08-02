using SimulcastUtility.Wpf.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Wpf.Themes
{
    public interface IThemeManager
    {
        IReadOnlyList<ThemeDefinition> Themes { get; }

        ThemeDefinition CurrentTheme { get; }

        event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        void RegisterTheme(ThemeDefinition theme);

        bool UnregisterTheme(string themeId);

        void ApplyTheme(string themeId);

        void ApplyDefaultTheme();
    }
}
