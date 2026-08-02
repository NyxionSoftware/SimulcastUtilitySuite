using SimulcastUtility.Wpf.Themes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Wpf.Events
{
    public sealed class ThemeChangedEventArgs : EventArgs
    {
        public ThemeDefinition PreviousTheme { get; }

        public ThemeDefinition CurrentTheme { get; }

        public ThemeChangedEventArgs(ThemeDefinition previousTheme, ThemeDefinition currentTheme)
        {
            PreviousTheme = previousTheme;
            CurrentTheme = currentTheme;
        }
    }
}
