using SimulcastUtility.Plugins.Interfaces;

namespace SimulcastUtility.Plugins.Models
{
    public sealed class PluginInfo : IPluginInfo
    {
        public PluginInfo(Guid pluginIdentifier, string name, string description, Version version, string author)
        {
            PluginIdentifier = pluginIdentifier;
            Name = name;
            Description = description;
            Version = version;
            Author = author;
        }

        public Guid PluginIdentifier { get; }

        public string Name { get; }

        public string Description { get; }

        public Version Version { get; }

        public string Author { get; }

        internal static PluginInfo CreateSnapshot(IPluginInfo pluginInfo)
        {
            return new PluginInfo(pluginInfo.PluginIdentifier, pluginInfo.Name, pluginInfo.Description, pluginInfo.Version, pluginInfo.Author);
        }
    }
}
