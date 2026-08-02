namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginInfo
    {
        Guid PluginIdentifier { get; }

        string Name { get; }

        string Description { get; }

        Version Version { get; }

        string Author { get; }
    }
}
