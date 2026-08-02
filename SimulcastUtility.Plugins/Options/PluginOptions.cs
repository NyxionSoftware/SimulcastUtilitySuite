using System.IO;

namespace SimulcastUtility.Plugins.Options
{
    public sealed class PluginOptions
    {
        public const string SectionName = "Plugins";

        public string Directory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimulcastUtility", "Plugins");

        public string DataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimulcastUtility", "PluginData");

        public string StateFileName { get; set; } = "plugin-state.json";

        public string GetStateFilePath()
        {
            return Path.Combine(Directory, StateFileName);
        }
    }
}
