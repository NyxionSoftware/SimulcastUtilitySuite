namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginBrandingManager
    {
        Task SetApplicationLogoAsync(Guid pluginIdentifier, Uri logoUri, CancellationToken cancellationToken = default);

        Task RemoveApplicationLogoAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default);
    }
}
