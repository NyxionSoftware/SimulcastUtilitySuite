namespace SimulcastUtility.Plugins.Models
{
    public sealed record PluginApplicationCommand(string Name, IReadOnlyDictionary<string, string?> Arguments);
}
