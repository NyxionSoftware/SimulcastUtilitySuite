using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;
using SimulcastUtility.Wpf.ViewModels.Views;
using System.Windows;
using System.Windows.Media;

namespace SimulcastUtility.Wpf.Services
{
    public sealed class WpfPluginUiManager : IPluginUiManager
    {
        private readonly Dictionary<Guid, List<Action>> _cleanupActions = new();
        private readonly ApplicationNavigationService _navigationService;
        private readonly ApplicationOverlayViewModel _overlayViewModel;
        private readonly List<WeakReference<FrameworkElement>> _visualRoots = new();

        public event EventHandler<PluginNotificationRequest>? NotificationRequested;

        public WpfPluginUiManager(ApplicationNavigationService navigationService, ApplicationOverlayViewModel overlayViewModel)
        {
            _navigationService = navigationService;
            _overlayViewModel = overlayViewModel;
        }

        public void RegisterVisualRoot(FrameworkElement visualRoot)
        {
            ArgumentNullException.ThrowIfNull(visualRoot);

            lock (_visualRoots)
            {
                _visualRoots.RemoveAll(reference => !reference.TryGetTarget(out _));

                if (_visualRoots.Any(reference => reference.TryGetTarget(out FrameworkElement? existingRoot) && ReferenceEquals(existingRoot, visualRoot)))
                    return;

                _visualRoots.Add(new WeakReference<FrameworkElement>(visualRoot));
            }
        }

        public async Task InvokeOnMainWindowAsync(Guid pluginIdentifier, Action<Window> action, CancellationToken cancellationToken = default)
        {
            ValidatePluginIdentifier(pluginIdentifier);
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => action(GetMainWindow()));
        }

        public async Task<TElement?> FindElementAsync<TElement>(Guid pluginIdentifier, string elementName, CancellationToken cancellationToken = default) where TElement : FrameworkElement
        {
            ValidatePluginIdentifier(pluginIdentifier);

            if (string.IsNullOrWhiteSpace(elementName))
                throw new ArgumentException("An element name is required.", nameof(elementName));

            cancellationToken.ThrowIfCancellationRequested();
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => FindRegisteredElement<TElement>(elementName));
        }

        public void RegisterCleanup(Guid pluginIdentifier, Action cleanupAction)
        {
            ValidatePluginIdentifier(pluginIdentifier);
            ArgumentNullException.ThrowIfNull(cleanupAction);

            lock (_cleanupActions)
            {
                if (!_cleanupActions.TryGetValue(pluginIdentifier, out List<Action>? actions))
                {
                    actions = new List<Action>();
                    _cleanupActions.Add(pluginIdentifier, actions);
                }

                actions.Add(cleanupAction);
            }
        }

        public Task ShowNotificationAsync(Guid pluginIdentifier, PluginNotificationRequest notification, CancellationToken cancellationToken = default)
        {
            ValidatePluginIdentifier(pluginIdentifier);
            ArgumentNullException.ThrowIfNull(notification);
            cancellationToken.ThrowIfCancellationRequested();
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                NotificationRequested?.Invoke(this, notification);

                switch (notification.Severity)
                {
                    case PluginNotificationSeverity.Success:
                        _overlayViewModel.ShowSuccess(notification.Title, notification.Message);
                        break;
                    case PluginNotificationSeverity.Error:
                        _overlayViewModel.ShowError(notification.Title, notification.Message);
                        break;
                    default:
                        _overlayViewModel.ShowInformation(notification.Title, notification.Message);
                        break;
                }
            }).Task;
        }

        public async Task<bool> ShowConfirmationAsync(Guid pluginIdentifier, string title, string message, CancellationToken cancellationToken = default)
        {
            ValidatePluginIdentifier(pluginIdentifier);
            cancellationToken.ThrowIfCancellationRequested();
            return await _overlayViewModel.ShowConfirmationAsync(pluginIdentifier, title, message, cancellationToken);
        }

        public async Task ShowDialogAsync(Guid pluginIdentifier, PluginDialogRequest dialog, CancellationToken cancellationToken = default)
        {
            ValidatePluginIdentifier(pluginIdentifier);
            ArgumentNullException.ThrowIfNull(dialog);
            cancellationToken.ThrowIfCancellationRequested();

            await _overlayViewModel.ShowDialogAsync(pluginIdentifier, dialog.Title, dialog.Content, dialog.Width, cancellationToken);
        }

        public Task NavigateToPageAsync(Guid pluginIdentifier, FrameworkElement page, CancellationToken cancellationToken = default)
        {
            ValidatePluginIdentifier(pluginIdentifier);
            ArgumentNullException.ThrowIfNull(page);
            cancellationToken.ThrowIfCancellationRequested();
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() => _navigationService.NavigateTo(page, pluginIdentifier: pluginIdentifier)).Task;
        }

        public Task NavigateBackAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default)
        {
            ValidatePluginIdentifier(pluginIdentifier);
            cancellationToken.ThrowIfCancellationRequested();
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(_navigationService.NavigateBack).Task;
        }

        public async Task RemovePluginUiAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _overlayViewModel.RemovePluginOverlays(pluginIdentifier);
                _navigationService.RemovePluginPages(pluginIdentifier);

                List<Action>? cleanupActions;

                lock (_cleanupActions)
                {
                    _cleanupActions.Remove(pluginIdentifier, out cleanupActions);
                }

                if (cleanupActions is null)
                    return;

                try
                {
                    for (int index = cleanupActions.Count - 1; index >= 0; index--)
                        cleanupActions[index]();
                }
                finally
                {
                    cleanupActions.Clear();
                }
            });
        }

        private static Window GetMainWindow()
        {
            return System.Windows.Application.Current.MainWindow ?? throw new InvalidOperationException("The main window is not available yet.");
        }

        private static TElement? FindElement<TElement>(DependencyObject parent, string elementName) where TElement : FrameworkElement
        {
            if (parent is TElement matchingElement && matchingElement.Name == elementName)
                return matchingElement;

            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                TElement? result = FindElement<TElement>(VisualTreeHelper.GetChild(parent, index), elementName);

                if (result is not null)
                    return result;
            }

            return null;
        }

        private TElement? FindRegisteredElement<TElement>(string elementName) where TElement : FrameworkElement
        {
            TElement? windowElement = FindElement<TElement>(GetMainWindow(), elementName);

            if (windowElement is not null)
                return windowElement;

            lock (_visualRoots)
            {
                _visualRoots.RemoveAll(reference => !reference.TryGetTarget(out _));

                foreach (WeakReference<FrameworkElement> reference in _visualRoots)
                {
                    if (reference.TryGetTarget(out FrameworkElement? visualRoot) && FindElement<TElement>(visualRoot, elementName) is { } matchingElement)
                        return matchingElement;
                }
            }

            return null;
        }

        private static void ValidatePluginIdentifier(Guid pluginIdentifier)
        {
            if (pluginIdentifier == Guid.Empty)
                throw new ArgumentException("A plugin identifier is required.", nameof(pluginIdentifier));
        }
    }
}
