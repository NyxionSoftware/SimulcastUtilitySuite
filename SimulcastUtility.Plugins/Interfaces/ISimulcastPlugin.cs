namespace SimulcastUtility.Plugins.Interfaces
{
    public interface ISimulcastPlugin
    {
        IPluginInfo Info { get; }

        Task InitializeAsync(IPluginContext pluginContext, CancellationToken cancellationToken = default);

        Task EnableAsync(CancellationToken cancellationToken = default);

        Task DisableAsync(CancellationToken cancellationToken = default);

        Task HandleApplicationArgumentsAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
    }
}
