using SimulcastUtility.Plugin.Abstractions.Events;
using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Shared.Commands;
using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using SimulcastUtility.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;

namespace SimulcastUtility.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly IReceiverControllerService _receiverController;

        private string _searchText = string.Empty;

        private string _pendingChannel = string.Empty;

        private bool _isSettingChannel;
        private bool _isRefreshing;
        private double _channelProgress;
        private bool _disposed;

        private string? _statusMessage;
        private bool _hasError;

        private Receiver? _subscribedReceiver;

        public ReadOnlyObservableCollection<Receiver> Receivers => _receiverController.Receivers;

        public ICollectionView FilteredReceivers { get; }

        public Receiver? SelectedReceiver
        {
            get => _receiverController.SelectedReceiver;

            set
            {
                if (ReferenceEquals(_receiverController.SelectedReceiver, value))
                {
                    return;
                }

                _receiverController.SelectedReceiver = value;

                SubscribeToReceiver(value);

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedReceiver));

                UpdateChannelProgress();

                RefreshCommands();
            }
        }

        public bool HasSelectedReceiver => SelectedReceiver is not null;

        public bool IsSettingChannel
        {
            get => _isSettingChannel;
            private set
            {
                if (_isSettingChannel == value)
                    return;

                _isSettingChannel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSetChannel));
            }
        }

        public bool CanSetChannel => SelectedReceiver?.CanExecuteActions == true && !IsSettingChannel;

        public string SearchText
        {
            get => _searchText;

            set
            {
                if (!SetField(ref _searchText, value))
                    return;

                FilteredReceivers.Refresh();
            }
        }

        public string PendingChannel
        {
            get => _pendingChannel;

            set => SetField(ref _pendingChannel, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (!SetField(ref _isRefreshing, value))
                    return;

                RefreshCommands();
            }
        }

        public double ChannelProgress
        {
            get => _channelProgress;
            private set => SetField(ref _channelProgress, value);
        }

        public string? StatusMessage
        {
            get => _statusMessage;

            private set
            {
                if (!SetField(ref _statusMessage, value))
                    return;

                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        public bool HasError
        {
            get => _hasError;
            private set => SetField(ref _hasError, value);
        }

        public ICommand ClearStatusCommand { get; }
        public ICommand RefreshAllCommand { get; }
        public ICommand RefreshSelectedCommand { get; }
        public ICommand SetChannelCommand { get; }
        public event EventHandler<bool>? ChannelChangedSuccessfully;

        public MainWindowViewModel(
            IReceiverControllerService receiverController)
        {
            _receiverController =
                receiverController
                ?? throw new ArgumentNullException(nameof(receiverController));

            FilteredReceivers =
                CollectionViewSource.GetDefaultView(
                    Receivers);

            FilteredReceivers.Filter =
                FilterReceiver;

            RefreshAllCommand =
                new AsyncRelayCommand(
                    RefreshAllAsync,
                    () => !IsRefreshing);

            RefreshSelectedCommand =
                new AsyncRelayCommand(
                    RefreshSelectedAsync,
                    () => SelectedReceiver is not null &&
                          !IsRefreshing);

            SetChannelCommand =
                new AsyncRelayCommand(
                    SetChannelAsync,
                    () => CanSetChannel);

            ClearStatusCommand = new RelayCommand(ClearStatus, () => true);

            SubscribeToController();

            if (SelectedReceiver is not null)
                SubscribeToReceiver(SelectedReceiver);
        }

        private void ShowError(string message)
        {
            HasError = true;
            StatusMessage = message;
        }

        private void ShowSuccess(string message)
        {
            HasError = false;
            StatusMessage = message;
        }

        private void ClearStatus()
        {
            StatusMessage = null;
            HasError = false;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _receiverController.InitializeAsync(cancellationToken);

            OnPropertyChanged(nameof(Receivers));
            OnPropertyChanged(nameof(SelectedReceiver));
            OnPropertyChanged(nameof(HasSelectedReceiver));

            FilteredReceivers.Refresh();

            if (SelectedReceiver is not null)
                SubscribeToReceiver(SelectedReceiver);

            UpdateChannelProgress();
            RefreshCommands();
        }

        private async Task RefreshAllAsync()
        {
            IsRefreshing = true;

            try
            {
                await _receiverController.RefreshAllReceiversAsync();

                UpdateChannelProgress();
                ClearStatus();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task RefreshSelectedAsync()
        {
            Receiver? receiver = SelectedReceiver;

            if (receiver is null)
                return;

            IsRefreshing = true;

            try
            {
                await _receiverController.RefreshReceiverAsync(receiver);

                UpdateChannelProgress();
                ClearStatus();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task SetChannelAsync()
        {
            if (IsSettingChannel)
                return;

            IsSettingChannel = true;
            Receiver? receiver = SelectedReceiver;

            try
            {
                if (receiver is null)
                {
                    ShowError("Select a receiver first.");
                    ChannelChangedSuccessfully?.Invoke(this, false);
                    return;
                }

                if (!TryNormalizeChannel(PendingChannel, out int serviceId))
                {
                    ShowError("Enter a channel from 1 through 50. Three-digit channels must begin with 1.");
                    ChannelChangedSuccessfully?.Invoke(this, false);
                    return;
                }

                CommandResult<HELLO_DISCOVERY_RESPONSE> result = await _receiverController.SendCommandAsync<HELLO_DISCOVERY_RESPONSE>(receiver, new FORCE_CH_SWITCH(serviceId), TimeSpan.FromSeconds(6));

                if (!result.IsSuccess)
                {
                    ShowError(result.ErrorMessage ?? "The channel command failed.");
                    PendingChannel = serviceId.ToString();
                    ChannelChangedSuccessfully?.Invoke(this, false);
                    return;
                }

                ClearStatus();
                PendingChannel = string.Empty;

                ChannelChangedSuccessfully?.Invoke(this, true);

                await Task.Delay(3000);

                await _receiverController.RefreshReceiverAsync(receiver, RefreshBehavior.WaitForRefreshWindow);
            }catch(Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                IsSettingChannel = false;
            }
        }

        private static bool TryNormalizeChannel(string? input, out int channel)
        {
            channel = 0;

            if (!int.TryParse(input, out int parsed))
            {
                return false;
            }

            channel = parsed switch
            {
                >= 1 and <= 50 => parsed + 100,

                >= 100 and <= 150 => parsed,

                _ => 0
            };

            return channel != 0;
        }

        private bool FilterReceiver(object item)
        {
            if (item is not Receiver receiver)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return receiver.Name.Contains(
                       SearchText,
                       StringComparison.OrdinalIgnoreCase) ||
                   receiver.ReceiverId.Contains(
                       SearchText,
                       StringComparison.OrdinalIgnoreCase) ||
                   receiver.IpAddress.Contains(
                       SearchText,
                       StringComparison.OrdinalIgnoreCase);
        }

        private void SubscribeToController()
        {
            _receiverController.ReceiversInitialized +=
                Controller_ReceiversChanged;

            _receiverController.ReceiverAdded +=
                Controller_ReceiverAdded;

            _receiverController.ReceiverRemoved +=
                Controller_ReceiverRemoved;

            _receiverController.ReceiverUpdated +=
                Controller_ReceiverUpdated;

            _receiverController.ReceiversReordered +=
                Controller_ReceiversChanged;

            _receiverController.SelectedReceiverChanged +=
                Controller_SelectedReceiverChanged;
        }

        private void Controller_SelectedReceiverChanged(
            object? sender,
            ReceiverEventArgs e)
        {
            SubscribeToReceiver(e.Receiver);

            OnPropertyChanged(nameof(SelectedReceiver));
            OnPropertyChanged(nameof(HasSelectedReceiver));

            UpdateChannelProgress();
            RefreshCommands();
            ClearStatus();
        }

        private void Controller_ReceiversChanged(
            object? sender,
            EventArgs e)
        {
            FilteredReceivers.Refresh();

            OnPropertyChanged(nameof(Receivers));
            OnPropertyChanged(nameof(SelectedReceiver));

            RefreshCommands();
        }

        private void Controller_ReceiverAdded(
            object? sender,
            ReceiverEventArgs e)
        {
            FilteredReceivers.Refresh();

            OnPropertyChanged(nameof(Receivers));

            RefreshCommands();
        }

        private void Controller_ReceiverRemoved(
            object? sender,
            ReceiverEventArgs e)
        {
            FilteredReceivers.Refresh();

            OnPropertyChanged(nameof(Receivers));
            OnPropertyChanged(nameof(SelectedReceiver));
            OnPropertyChanged(nameof(HasSelectedReceiver));

            RefreshCommands();
        }

        private void Controller_ReceiverUpdated(
            object? sender,
            ReceiverUpdatedEventArgs e)
        {
            FilteredReceivers.Refresh();

            if (ReferenceEquals(
                    SelectedReceiver,
                    e.Receiver))
            {
                UpdateChannelProgress();
            }

            RefreshCommands();
        }

        private void SubscribeToReceiver(Receiver? receiver)
        {
            if (ReferenceEquals(_subscribedReceiver, receiver))
                return;

            if (_subscribedReceiver is not null)
            {
                _subscribedReceiver.PropertyChanged -= Receiver_PropertyChanged;
            }

            _subscribedReceiver = receiver;

            if (_subscribedReceiver is not null)
            {
                _subscribedReceiver.PropertyChanged += Receiver_PropertyChanged;
            }
        }

        private void Receiver_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is
                nameof(Receiver.Status) or
                nameof(Receiver.CanExecuteActions))
            {
                RefreshCommands();
            }

            if (e.PropertyName is
                nameof(Receiver.ChannelStartTime) or
                nameof(Receiver.ChannelEndTime) or
                nameof(Receiver.ChannelDuration) or
                nameof(Receiver.ChannelRemainingTime))
            {
                UpdateChannelProgress();
            }
        }

        public void UpdateChannelProgress()
        {
            Receiver? receiver =
                SelectedReceiver;

            if (receiver?.ChannelStartTime is not DateTime start ||
                receiver.ChannelEndTime is not DateTime end ||
                end <= start)
            {
                ChannelProgress =
                    0;

                return;
            }

            DateTime now =
                DateTime.UtcNow;

            double totalMilliseconds =
                (end - start).TotalMilliseconds;

            double elapsedMilliseconds =
                (now - start).TotalMilliseconds;

            ChannelProgress =
                Math.Clamp(
                    elapsedMilliseconds / totalMilliseconds * 100,
                    0,
                    100);
        }

        private void RefreshCommands()
        {
            (RefreshAllCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

            (RefreshSelectedCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

            (SetChannelCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _receiverController.ReceiversInitialized -= Controller_ReceiversChanged;

            _receiverController.ReceiverAdded -= Controller_ReceiverAdded;

            _receiverController.ReceiverRemoved -= Controller_ReceiverRemoved;

            _receiverController.ReceiverUpdated -= Controller_ReceiverUpdated;

            _receiverController.ReceiversReordered -= Controller_ReceiversChanged;

            _receiverController.SelectedReceiverChanged -= Controller_SelectedReceiverChanged;

            foreach (Receiver receiver in Receivers)
            {
                receiver.PropertyChanged -= Receiver_PropertyChanged;
            }

            SubscribeToReceiver(null);
        }
    }
}
