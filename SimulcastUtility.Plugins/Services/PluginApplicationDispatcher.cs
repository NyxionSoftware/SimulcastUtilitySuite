using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;

namespace SimulcastUtility.Plugins.Services
{
    public sealed class PluginApplicationDispatcher : IPluginApplicationDispatcher
    {
        public event EventHandler<PluginApplicationCommand>? CommandDispatched;

        public Task DispatchAsync(PluginApplicationCommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();
            CommandDispatched?.Invoke(this, command);
            return Task.CompletedTask;
        }
    }
}
