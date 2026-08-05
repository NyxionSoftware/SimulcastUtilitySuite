using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using SimulcastUtility.Wpf.Options;
using SimulcastUtility.Wpf.ViewModels.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class ApplicationOverlayViewModel : ObservableObject
    {
        private readonly IOptionsMonitor<NotificationOptions> _notificationOptions;
        private readonly Queue<ModalRequest> _modalQueue = new();
        private readonly ObservableCollection<NotificationViewModel> _notifications = new();
        private ModalRequest? _currentModal;

        public ReadOnlyObservableCollection<NotificationViewModel> Notifications { get; }

        public IRelayCommand<NotificationViewModel> DismissNotificationCommand { get; }

        public IRelayCommand ConfirmModalCommand { get; }

        public IRelayCommand CancelModalCommand { get; }

        public string ModalTitle => _currentModal?.Title ?? string.Empty;

        public string ModalMessage => _currentModal?.Message ?? string.Empty;

        public FrameworkElement? ModalContent => _currentModal?.Content;

        public Visibility ModalVisibility => _currentModal is null ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ConfirmationActionsVisibility => _currentModal?.IsConfirmation == true ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CloseActionVisibility => _currentModal is not null && !_currentModal.IsConfirmation ? Visibility.Visible : Visibility.Collapsed;

        public double ModalWidth => _currentModal?.Width ?? 430;

        public ApplicationOverlayViewModel(IOptionsMonitor<NotificationOptions> notificationOptions)
        {
            _notificationOptions = notificationOptions;
            Notifications = new ReadOnlyObservableCollection<NotificationViewModel>(_notifications);
            DismissNotificationCommand = new RelayCommand<NotificationViewModel>(DismissNotification);
            ConfirmModalCommand = new RelayCommand(() => CompleteCurrentModal(true));
            CancelModalCommand = new RelayCommand(() => CompleteCurrentModal(false));
        }

        public void ShowSuccess(string title, string message) => AddNotification(new NotificationViewModel(title, message, NotificationSeverity.Success, _notificationOptions.CurrentValue.GetDisplayDuration()));

        public void ShowInformation(string title, string message) => AddNotification(new NotificationViewModel(title, message, NotificationSeverity.Info, _notificationOptions.CurrentValue.GetDisplayDuration()));

        public void ShowError(string title, string message) => AddNotification(new NotificationViewModel(title, message, NotificationSeverity.Error, _notificationOptions.CurrentValue.GetDisplayDuration()));

        public async Task<bool> ShowConfirmationAsync(Guid? pluginIdentifier, string title, string message, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ModalRequest request = new(pluginIdentifier, title, message, null, true, completion, 430);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => EnqueueModal(request));
            using CancellationTokenRegistration registration = cancellationToken.Register(() => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => CancelModalRequest(request, cancellationToken)));
            return await completion.Task;
        }

        public Task ShowDialogAsync(Guid? pluginIdentifier, string title, FrameworkElement content, double width, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ModalRequest request = new(pluginIdentifier, title, string.Empty, content, false, null, width);
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() => EnqueueModal(request)).Task;
        }

        public void RemovePluginOverlays(Guid pluginIdentifier)
        {
            if (_modalQueue.Count > 0)
            {
                ModalRequest[] retained = _modalQueue.Where(request => request.PluginIdentifier != pluginIdentifier).ToArray();

                foreach (ModalRequest removed in _modalQueue.Where(request => request.PluginIdentifier == pluginIdentifier))
                    removed.Completion?.TrySetResult(false);

                _modalQueue.Clear();

                foreach (ModalRequest request in retained)
                    _modalQueue.Enqueue(request);
            }

            if (_currentModal?.PluginIdentifier == pluginIdentifier)
                CompleteCurrentModal(false);
        }

        private void EnqueueModal(ModalRequest request)
        {
            if (_currentModal is null)
            {
                SetCurrentModal(request);
                return;
            }

            _modalQueue.Enqueue(request);
        }

        private void CompleteCurrentModal(bool result)
        {
            ModalRequest? completed = _currentModal;
            SetCurrentModal(_modalQueue.TryDequeue(out ModalRequest? next) ? next : null);
            completed?.Completion?.TrySetResult(result);
        }

        private void CancelModalRequest(ModalRequest request, CancellationToken cancellationToken)
        {
            if (ReferenceEquals(_currentModal, request))
            {
                SetCurrentModal(_modalQueue.TryDequeue(out ModalRequest? next) ? next : null);
                request.Completion?.TrySetCanceled(cancellationToken);
                return;
            }

            if (_modalQueue.Count > 0)
            {
                ModalRequest[] retained = _modalQueue.Where(queuedRequest => !ReferenceEquals(queuedRequest, request)).ToArray();
                _modalQueue.Clear();

                foreach (ModalRequest retainedRequest in retained)
                    _modalQueue.Enqueue(retainedRequest);
            }

            request.Completion?.TrySetCanceled(cancellationToken);
        }

        private void SetCurrentModal(ModalRequest? request)
        {
            _currentModal = request;
            OnPropertyChanged(nameof(ModalTitle));
            OnPropertyChanged(nameof(ModalMessage));
            OnPropertyChanged(nameof(ModalContent));
            OnPropertyChanged(nameof(ModalVisibility));
            OnPropertyChanged(nameof(ConfirmationActionsVisibility));
            OnPropertyChanged(nameof(CloseActionVisibility));
            OnPropertyChanged(nameof(ModalWidth));
        }

        private void AddNotification(NotificationViewModel notification)
        {
            System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => AddNotification(notification));
                return;
            }

            _notifications.Add(notification);
            _ = DismissNotificationAfterDelayAsync(notification);
        }

        private void DismissNotification(NotificationViewModel? notification)
        {
            if (notification is not null)
                _notifications.Remove(notification);
        }

        private async Task DismissNotificationAfterDelayAsync(NotificationViewModel notification)
        {
            await Task.Delay(notification.DisplayDuration);
            System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;

            if (!dispatcher.HasShutdownStarted)
                await dispatcher.InvokeAsync(() => _notifications.Remove(notification));
        }

        private sealed record ModalRequest(Guid? PluginIdentifier, string Title, string Message, FrameworkElement? Content, bool IsConfirmation, TaskCompletionSource<bool>? Completion, double Width);
    }
}
