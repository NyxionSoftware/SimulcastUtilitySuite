using SimulcastUtility.Plugins.Interfaces;

namespace SimulcastUtility.Plugins.Models
{
    public sealed class LoadedPlugin
    {
        internal LoadedPlugin(PluginInfo info, string assemblyPath, bool isEnabled, bool hasSettings, string? error)
        {
            Info = info;
            AssemblyPath = assemblyPath;
            IsEnabled = isEnabled;
            HasSettings = hasSettings;
            Error = error;
        }

        public PluginInfo Info { get; }

        public string AssemblyPath { get; }

        public bool IsEnabled { get; internal set; }

        public bool HasSettings { get; }

        public string? Error { get; internal set; }
    }
}
