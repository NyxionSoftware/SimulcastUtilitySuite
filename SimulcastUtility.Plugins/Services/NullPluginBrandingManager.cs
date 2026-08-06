using SimulcastUtility.Plugins.Interfaces;

namespace SimulcastUtility.Plugins.Services
{
    internal sealed class NullPluginBrandingManager : IPluginBrandingManager
    {
        public Task SetApplicationLogoAsync(Guid pluginIdentifier, Uri logoUri, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveApplicationLogoAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
