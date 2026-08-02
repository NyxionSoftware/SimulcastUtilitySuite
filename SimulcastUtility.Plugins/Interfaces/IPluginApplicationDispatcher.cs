using SimulcastUtility.Plugins.Models;

namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginApplicationDispatcher
    {
        event EventHandler<PluginApplicationCommand>? CommandDispatched;

        Task DispatchAsync(PluginApplicationCommand command, CancellationToken cancellationToken = default);
    }
}
