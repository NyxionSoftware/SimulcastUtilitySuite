namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginThemeManager
    {
        Task ApplyResourceDictionaryAsync(Guid pluginIdentifier, Uri resourceDictionaryUri, CancellationToken cancellationToken = default);

        Task RemoveResourceDictionaryAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default);

        Task SetWindowChromeModeAsync(Guid pluginIdentifier, SimulcastUtility.Plugins.Models.PluginWindowChromeMode mode, CancellationToken cancellationToken = default);
    }
}
