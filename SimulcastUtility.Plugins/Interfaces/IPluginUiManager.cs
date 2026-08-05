using SimulcastUtility.Plugins.Models;
using System.Windows;

namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginUiManager
    {
        event EventHandler<PluginNotificationRequest>? NotificationRequested;

        void RegisterVisualRoot(FrameworkElement visualRoot);

        Task InvokeOnMainWindowAsync(Guid pluginIdentifier, Action<Window> action, CancellationToken cancellationToken = default);

        Task<TElement?> FindElementAsync<TElement>(Guid pluginIdentifier, string elementName, CancellationToken cancellationToken = default) where TElement : FrameworkElement;

        void RegisterCleanup(Guid pluginIdentifier, Action cleanupAction);

        Task ShowNotificationAsync(Guid pluginIdentifier, PluginNotificationRequest notification, CancellationToken cancellationToken = default);

        Task<bool> ShowConfirmationAsync(Guid pluginIdentifier, string title, string message, CancellationToken cancellationToken = default);

        Task ShowDialogAsync(Guid pluginIdentifier, PluginDialogRequest dialog, CancellationToken cancellationToken = default);

        Task NavigateToPageAsync(Guid pluginIdentifier, FrameworkElement page, CancellationToken cancellationToken = default);

        Task NavigateBackAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default);

        Task RemovePluginUiAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default);
    }
}
