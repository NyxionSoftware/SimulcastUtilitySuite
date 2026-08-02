namespace SimulcastUtility.Plugins.Models
{
    public sealed record PluginImportResult(string Directory, int ImportedFileCount, int LoadedPluginCount);
}
