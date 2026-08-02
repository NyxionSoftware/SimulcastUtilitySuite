using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Wpf.Themes
{
    public sealed record ThemeDefinition(string Id, string Name, Uri ResourceUri, Guid? PluginId = null);
}
