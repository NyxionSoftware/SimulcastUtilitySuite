using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulcastUtility.Application.Events;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Requests;
using SimulcastUtility.Core.Enums;
using SimulcastUtility.Core.Models;
using SimulcastUtility.Wpf.ViewModels.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class ReceiverManagerViewModel : ObservableObject, IDisposable
    {
        private readonly IReceiverManager _receiverManager;
        private readonly IReceiverCommandManager _receiverCommandManager;
        private readonly MainViewModel _notificationHost;
        private ReceiverConfigurationItemViewModel? _selectedReceiver;
        private ReceiverConfigurationItemViewModel? _pendingNewReceiver;
        private Receiver? _editingReceiver;
        private TaskCompletionSource<bool>? _confirmationCompletionSource;
        private string _busyMessage = string.Empty;
        private string _confirmationTitle = string.Empty;
        private string _confirmationMessage = string.Empty;
        private bool _isBusy;
        private bool _hasUnsavedChanges;
        private bool _isConfirmationVisible;
        private bool _suppressChangeTracking;

        public ObservableCollection<ReceiverConfigurationItemViewModel> Receivers { get; } = new();

        public ReadOnlyObservableCollection<NotificationViewModel> Notifications => _notificationHost.Notifications;

        public IRelayCommand<NotificationViewModel> DismissNotificationCommand => _notificationHost.DismissNotificationCommand;

        public ReceiverConfigurationItemViewModel? SelectedReceiver
        {
            get => _selectedReceiver;
            set
            {
                if (!ReferenceEquals(_selectedReceiver, value))
                    DiscardReceiverEdits(_selectedReceiver);

                if (!ReferenceEquals(value, _pendingNewReceiver))
                    DiscardPendingReceiver();

                if (!SetProperty(ref _selectedReceiver, value))
                    return;

                UpdateEditingReceiver(value?.Source);
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(EditorDescription));
                DeleteReceiverCommand.NotifyCanExecuteChanged();
            }
        }

        public string EditorTitle => SelectedReceiver?.IsNew == true ? "Add Receiver" : "Edit Receiver";

        public string EditorDescription => SelectedReceiver is null ? "Select a receiver or add a new one." : "Update the receiver configuration below.";

        public string BusyMessage
        {
            get => _busyMessage;
            private set => SetProperty(ref _busyMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (!SetProperty(ref _isBusy, value))
                    return;

                OnPropertyChanged(nameof(BusyVisibility));
                SaveReceiverCommand.NotifyCanExecuteChanged();
                DeleteReceiverCommand.NotifyCanExecuteChanged();
            }
        }

        public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set
            {
                if (!SetProperty(ref _hasUnsavedChanges, value))
                    return;

                SaveReceiverCommand.NotifyCanExecuteChanged();
            }
        }

        public string ConfirmationTitle
        {
            get => _confirmationTitle;
            private set => SetProperty(ref _confirmationTitle, value);
        }

        public string ConfirmationMessage
        {
            get => _confirmationMessage;
            private set => SetProperty(ref _confirmationMessage, value);
        }

        public bool IsConfirmationVisible
        {
            get => _isConfirmationVisible;
            private set
            {
                if (!SetProperty(ref _isConfirmationVisible, value))
                    return;

                OnPropertyChanged(nameof(ConfirmationVisibility));
            }
        }

        public Visibility ConfirmationVisibility => IsConfirmationVisible ? Visibility.Visible : Visibility.Collapsed;

        public IRelayCommand AddReceiverCommand { get; }

        public IAsyncRelayCommand DeleteReceiverCommand { get; }

        public IAsyncRelayCommand SaveReceiverCommand { get; }

        public IAsyncRelayCommand BackCommand { get; }

        public IRelayCommand ConfirmCommand { get; }

        public IRelayCommand CancelConfirmationCommand { get; }

        public event EventHandler? CloseRequested;

        public ReceiverManagerViewModel(IReceiverManager receiverManager, IReceiverCommandManager receiverCommandManager, MainViewModel notificationHost, Guid? receiverToEdit = null, bool beginAdd = false)
        {
            _receiverManager = receiverManager;
            _receiverCommandManager = receiverCommandManager;
            _notificationHost = notificationHost;

            AddReceiverCommand = new RelayCommand(BeginAdd);
            DeleteReceiverCommand = new AsyncRelayCommand(DeleteSelectedReceiverAsync, () => SelectedReceiver is { IsNew: false } && !IsBusy);
            SaveReceiverCommand = new AsyncRelayCommand(SaveReceiverAsync, CanSaveReceiver);
            BackCommand = new AsyncRelayCommand(BackAsync);
            ConfirmCommand = new RelayCommand(() => CompleteConfirmation(true));
            CancelConfirmationCommand = new RelayCommand(() => CompleteConfirmation(false));

            _receiverCommandManager.ReceiverConnectionStatusChanged += ReceiverStatusChanged;
            LoadReceiverDrafts();

            if (beginAdd)
                BeginAdd();
            else
                SelectedReceiver = receiverToEdit is { } id ? Receivers.FirstOrDefault(receiver => receiver.Id == id) : Receivers.FirstOrDefault();
        }

        private void BeginAdd()
        {
            if (_pendingNewReceiver is not null)
                UntrackReceiver(_pendingNewReceiver);

            ReceiverConfigurationItemViewModel receiver = new();
            TrackReceiver(receiver);
            _pendingNewReceiver = receiver;
            SelectedReceiver = receiver;
            SaveReceiverCommand.NotifyCanExecuteChanged();
        }

        public void PreviewReceiverMove(ReceiverConfigurationItemViewModel receiver, int targetIndex)
        {
            int currentIndex = Receivers.IndexOf(receiver);

            if (currentIndex < 0)
                return;

            targetIndex = Math.Clamp(targetIndex, 0, Receivers.Count);

            if (targetIndex > currentIndex)
                targetIndex--;

            if (targetIndex == currentIndex)
                return;

            Receivers.Move(currentIndex, targetIndex);
        }

        public async Task PersistReceiverMoveAsync(ReceiverConfigurationItemViewModel receiver, int originalIndex)
        {
            int currentIndex = Receivers.IndexOf(receiver);

            if (currentIndex < 0 || receiver.Id is not { } receiverId || currentIndex == originalIndex)
                return;

            try
            {
                await _receiverManager.MoveReceiverAsync(receiverId, currentIndex);
            }
            catch (Exception ex)
            {
                int movedIndex = Receivers.IndexOf(receiver);

                if (movedIndex >= 0)
                    RestoreReceiverPosition(receiver, originalIndex);

                _notificationHost.ShowError("Receiver order not saved", ex.Message);
            }
        }

        public void RestoreReceiverPosition(ReceiverConfigurationItemViewModel receiver, int originalIndex)
        {
            int currentIndex = Receivers.IndexOf(receiver);

            if (currentIndex >= 0 && currentIndex != originalIndex)
                Receivers.Move(currentIndex, Math.Clamp(originalIndex, 0, Receivers.Count - 1));
        }

        private async Task DeleteSelectedReceiverAsync()
        {
            if (SelectedReceiver is not { } receiver)
                return;

            if (!await RequestConfirmationAsync("Delete receiver?", $"Are you sure you want to permanently delete '{receiver.Name}'?"))
                return;

            if (receiver.Id is not { } receiverId)
                return;

            IsBusy = true;
            BusyMessage = $"Deleting {receiver.Name}...";
            FinishEditingReceiver();

            try
            {
                await _receiverManager.RemoveReceiverAsync(receiverId);

                int index = Receivers.IndexOf(receiver);
                UntrackReceiver(receiver);
                Receivers.Remove(receiver);
                SelectedReceiver = Receivers.Count == 0 ? null : Receivers[Math.Clamp(index, 0, Receivers.Count - 1)];
                RecalculateHasUnsavedChanges();
                _notificationHost.ShowSuccess("Receiver deleted", $"{receiver.Name} was deleted.");
            }
            catch (Exception ex)
            {
                UpdateEditingReceiver(receiver.Source);
                _notificationHost.ShowError("Receiver not deleted", ex.Message);
            }
            finally
            {
                BusyMessage = string.Empty;
                IsBusy = false;
            }
        }

        private async Task SaveReceiverAsync(CancellationToken cancellationToken)
        {
            if (SelectedReceiver is not { } receiver)
                return;

            IsBusy = true;
            BusyMessage = $"Validating {receiver.Name}...";
            _notificationHost.ShowInfo("Saving receiver", $"{receiver.Name} is being validated.");

            try
            {
                ValidateReceiver(receiver);
                Receiver? savedReceiver;

                if (ReferenceEquals(receiver, _pendingNewReceiver))
                {
                    BusyMessage = $"Discovering {receiver.Name}...";
                    await _receiverCommandManager.VerifyReceiverAsync(receiver.IpAddress.Trim(), receiver.ReceiverId.Trim(), cancellationToken);
                    BusyMessage = $"Saving {receiver.Name}...";
                    savedReceiver = await _receiverManager.AddReceiverAsync(new ReceiverCreateRequest(receiver.Name.Trim(), receiver.ReceiverId.Trim(), receiver.IpAddress.Trim()), cancellationToken);
                }
                else
                {
                    if (RequiresDiscoveryVerification(receiver))
                    {
                        BusyMessage = $"Verifying {receiver.Name}...";
                        await _receiverCommandManager.VerifyReceiverAsync(receiver.IpAddress.Trim(), receiver.ReceiverId.Trim(), cancellationToken);
                    }

                    BusyMessage = $"Saving {receiver.Name}...";
                    savedReceiver = await _receiverManager.UpdateReceiverAsync(receiver.Id!.Value, new ReceiverUpdateRequest(receiver.Name.Trim(), receiver.ReceiverId.Trim(), receiver.IpAddress.Trim()), cancellationToken);
                }

                LoadReceiverDrafts();
                SelectedReceiver = Receivers.FirstOrDefault(receiver => receiver.Id == savedReceiver.Id);
                FinishEditingReceiver();
                HasUnsavedChanges = false;
                _notificationHost.ShowSuccess("Receiver saved", $"{savedReceiver.Configuration.Name} was saved successfully.");

                if (receiver.IsNew)
                {
                    BusyMessage = $"Refreshing {savedReceiver.Configuration.Name}...";

                    try
                    {
                        await _receiverCommandManager.RefreshReceiverAsync(savedReceiver.Id, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _notificationHost.ShowError("Receiver refresh failed", ex.Message);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _notificationHost.ShowError("Receiver changes not saved", ex.Message);
            }
            finally
            {
                BusyMessage = string.Empty;
                IsBusy = false;
            }
        }

        private async Task BackAsync()
        {
            if (HasUnsavedChanges && !await RequestConfirmationAsync("Discard unsaved changes?", "You have unsaved receiver changes. Are you sure you want to go back without saving?"))
                return;

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private Task<bool> RequestConfirmationAsync(string title, string message)
        {
            ConfirmationTitle = title;
            ConfirmationMessage = message;
            IsConfirmationVisible = true;
            _confirmationCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _confirmationCompletionSource.Task;
        }

        private void CompleteConfirmation(bool confirmed)
        {
            IsConfirmationVisible = false;
            TaskCompletionSource<bool>? completionSource = _confirmationCompletionSource;
            _confirmationCompletionSource = null;
            completionSource?.TrySetResult(confirmed);
        }

        private void LoadReceiverDrafts()
        {
            _suppressChangeTracking = true;

            foreach (ReceiverConfigurationItemViewModel receiver in Receivers)
                UntrackReceiver(receiver);

            if (_pendingNewReceiver is not null)
            {
                UntrackReceiver(_pendingNewReceiver);
                _pendingNewReceiver = null;
            }

            Receivers.Clear();
            foreach (Receiver receiver in _receiverManager.Receivers)
            {
                ReceiverConfigurationItemViewModel draft = new(receiver);
                TrackReceiver(draft);
                Receivers.Add(draft);
            }

            _suppressChangeTracking = false;
        }

        private void TrackReceiver(ReceiverConfigurationItemViewModel receiver)
        {
            receiver.PropertyChanged += ReceiverPropertyChanged;
        }

        private void UntrackReceiver(ReceiverConfigurationItemViewModel receiver)
        {
            receiver.PropertyChanged -= ReceiverPropertyChanged;
        }

        private void DiscardPendingReceiver()
        {
            if (_pendingNewReceiver is null)
                return;

            UntrackReceiver(_pendingNewReceiver);
            _pendingNewReceiver = null;
            RecalculateHasUnsavedChanges();
            SaveReceiverCommand.NotifyCanExecuteChanged();
        }

        private void DiscardReceiverEdits(ReceiverConfigurationItemViewModel? receiver)
        {
            if (receiver?.Source is null)
                return;

            _suppressChangeTracking = true;
            receiver.ResetFromSource();
            _suppressChangeTracking = false;
            RecalculateHasUnsavedChanges();
            SaveReceiverCommand.NotifyCanExecuteChanged();
        }

        private void ReceiverPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_suppressChangeTracking && e.PropertyName is nameof(ReceiverConfigurationItemViewModel.Name) or nameof(ReceiverConfigurationItemViewModel.ReceiverId) or nameof(ReceiverConfigurationItemViewModel.IpAddress))
            {
                RecalculateHasUnsavedChanges();
                SaveReceiverCommand.NotifyCanExecuteChanged();

                if (sender is ReceiverConfigurationItemViewModel { Source: not null } receiver)
                    UpdateEditingReceiver(receiver.Source);
            }
        }

        private bool CanSaveReceiver()
        {
            return !IsBusy && HasUnsavedChanges && SelectedReceiver?.IsValid == true;
        }

        private void RecalculateHasUnsavedChanges()
        {
            bool hasPendingReceiverChanges = _pendingNewReceiver is not null && (!string.IsNullOrWhiteSpace(_pendingNewReceiver.Name) || !string.IsNullOrWhiteSpace(_pendingNewReceiver.ReceiverId) || !string.IsNullOrWhiteSpace(_pendingNewReceiver.IpAddress));
            bool hasConfigurationChanges = SelectedReceiver?.Source is not null && (SelectedReceiver.Name != SelectedReceiver.Source.Configuration.Name || SelectedReceiver.ReceiverId != SelectedReceiver.Source.Configuration.ReceiverId || SelectedReceiver.IpAddress != SelectedReceiver.Source.Configuration.IpAddress);
            HasUnsavedChanges = hasPendingReceiverChanges || hasConfigurationChanges;
        }

        private void UpdateEditingReceiver(Receiver? receiver)
        {
            if (ReferenceEquals(_editingReceiver, receiver))
                return;

            if (_editingReceiver is not null && _editingReceiver.ActivityStatus == ReceiverActivityStatus.Editing && _receiverManager.GetReceiver(_editingReceiver.Id) is not null)
                _receiverCommandManager.SetReceiverActivityStatus(_editingReceiver.Id, ReceiverActivityStatus.Idle);

            _editingReceiver = receiver;

            if (_editingReceiver is not null)
                _receiverCommandManager.SetReceiverActivityStatus(_editingReceiver.Id, ReceiverActivityStatus.Editing);
        }

        private void FinishEditingReceiver()
        {
            if (_editingReceiver is not null && _editingReceiver.ActivityStatus == ReceiverActivityStatus.Editing && _receiverManager.GetReceiver(_editingReceiver.Id) is not null)
                _receiverCommandManager.SetReceiverActivityStatus(_editingReceiver.Id, ReceiverActivityStatus.Idle);

            _editingReceiver = null;
        }

        private static void ValidateReceiver(ReceiverConfigurationItemViewModel receiver)
        {
            if (!receiver.IsNameValid)
                throw new InvalidOperationException("Every receiver must have a name.");

            if (!receiver.IsReceiverIdValid)
                throw new InvalidOperationException($"{receiver.Name} must have a numeric receiver ID.");

            if (!receiver.IsIpAddressValid)
                throw new InvalidOperationException($"{receiver.Name} must have a valid four-octet IPv4 address.");
        }

        private static bool RequiresDiscoveryVerification(ReceiverConfigurationItemViewModel receiver)
        {
            return receiver.Source is not null && (receiver.ReceiverId.Trim() != receiver.Source.Configuration.ReceiverId || receiver.IpAddress.Trim() != receiver.Source.Configuration.IpAddress);
        }

        private void ReceiverStatusChanged(object? sender, ReceiverChangedEventArgs e)
        {
            Receivers.FirstOrDefault(receiver => ReferenceEquals(receiver.Source, e.Receiver))?.RefreshStatus();
        }

        public void Dispose()
        {
            UpdateEditingReceiver(null);
            _receiverCommandManager.ReceiverConnectionStatusChanged -= ReceiverStatusChanged;

            foreach (ReceiverConfigurationItemViewModel receiver in Receivers)
                UntrackReceiver(receiver);

            if (_pendingNewReceiver is not null)
                UntrackReceiver(_pendingNewReceiver);
        }
    }
}
