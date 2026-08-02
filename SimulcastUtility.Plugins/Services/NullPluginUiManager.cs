using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;
using System.Windows;

namespace SimulcastUtility.Plugins.Services
{
    internal sealed class NullPluginUiManager : IPluginUiManager
    {
        public event EventHandler<PluginNotificationRequest>? NotificationRequested
        {
            add { }
            remove { }
        }

        public void RegisterVisualRoot(FrameworkElement visualRoot)
        {
        }

        public Task InvokeOnMainWindowAsync(Guid pluginIdentifier, Action<Window> action, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TElement?> FindElementAsync<TElement>(Guid pluginIdentifier, string elementName, CancellationToken cancellationToken = default) where TElement : FrameworkElement => Task.FromResult<TElement?>(null);

        public void RegisterCleanup(Guid pluginIdentifier, Action cleanupAction)
        {
        }

        public Task ShowNotificationAsync(Guid pluginIdentifier, PluginNotificationRequest notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ShowConfirmationAsync(Guid pluginIdentifier, string title, string message, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task ShowDialogAsync(Guid pluginIdentifier, PluginDialogRequest dialog, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemovePluginUiAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
