using SimulcastUtility.Plugins.Interfaces;

namespace SimulcastUtility.Plugins.Services
{
    internal sealed class NullPluginThemeManager : IPluginThemeManager
    {
        public Task ApplyResourceDictionaryAsync(Guid pluginIdentifier, Uri resourceDictionaryUri, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveResourceDictionaryAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SetWindowChromeModeAsync(Guid pluginIdentifier, SimulcastUtility.Plugins.Models.PluginWindowChromeMode mode, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
