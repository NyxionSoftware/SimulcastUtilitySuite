using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulcastUtility.Application.Events;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Protocol.Commands;
using SimulcastUtility.Application.Protocol.Payloads;
using SimulcastUtility.Core.Enums;
using SimulcastUtility.Core.Models;
using SimulcastUtility.Wpf.ViewModels.Models;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Data;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly IReceiverManager _receiverManager;
        private readonly IReceiverCommandManager _receiverCommandManager;
        private readonly ApplicationOverlayViewModel _overlayViewModel;
        private readonly ObservableCollection<ReceiverViewModel> _receivers = new();
        private readonly ConcurrentDictionary<string, byte> _displayedReceiverErrors = new();
        private readonly ICollectionView _filteredReceivers;
        private readonly object _refreshCooldownLock = new();
        private readonly HashSet<Guid> _refreshingReceiverIds = new();
        private readonly System.Windows.Threading.DispatcherTimer _channelProgressTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private static readonly TimeSpan RefreshReceiverCooldown = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RefreshAllReceiversCooldown = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ChannelValidationDisplayDuration = TimeSpan.FromSeconds(1.5);

        private ReceiverViewModel? _selectedReceiver;
        private string _searchText = string.Empty;
        private string _pendingChannel = string.Empty;
        private string? _channelStatusMessage;
        private int _loadedPluginCount;
        private DateTimeOffset _nextRefreshAllAtUtc = DateTimeOffset.MinValue;
        private bool _isRefreshingAllReceivers;
        private bool _suppressErrorUpdates;
        private bool _isChannelValid;
        private bool _isChannelInvalid;
        private CancellationTokenSource? _channelValidationCancellationTokenSource;

        public ReadOnlyObservableCollection<ReceiverViewModel> Receivers { get; }

        public ICollectionView FilteredReceivers => _filteredReceivers;

        public IAsyncRelayCommand RefreshAllReceiversCommand { get; }

        public IAsyncRelayCommand RefreshSelectedReceiverCommand { get; }

        public IAsyncRelayCommand SetChannelCommand { get; }

        public IRelayCommand AddReceiverCommand { get; }

        public IRelayCommand ManageReceiversCommand { get; }

        public IRelayCommand VirtualRemoteCommand { get; }

        public IRelayCommand EditReceiverCommand { get; }

        public IRelayCommand ManagePluginsCommand { get; }

        public IRelayCommand SettingsCommand { get; }

        public event EventHandler? AddReceiverRequested;

        public event EventHandler? ManageReceiversRequested;

        public event Action<ReceiverViewModel>? VirtualRemoteRequested;

        public event Action<ReceiverViewModel>? EditReceiverRequested;

        public event EventHandler? ManagePluginsRequested;

        public event EventHandler? SettingsRequested;

        public ReceiverViewModel? SelectedReceiver
        {
            get => _selectedReceiver;
            set
            {
                if (_selectedReceiver?.Id == value?.Id)
                    return;

                _receiverManager.SelectReceiver(value?.Id);
            }
        }

        public bool HasSelectedReceiver => SelectedReceiver is not null;

        public bool IsChannelValid
        {
            get => _isChannelValid;
            private set => SetProperty(ref _isChannelValid, value);
        }

        public bool IsChannelInvalid
        {
            get => _isChannelInvalid;
            private set => SetProperty(ref _isChannelInvalid, value);
        }

        public bool IsRefreshingReceiver => SelectedReceiver is { } receiver && IsReceiverRefreshing(receiver.Id);

        public bool IsRefreshingAllReceivers
        {
            get => _isRefreshingAllReceivers;
            private set => SetProperty(ref _isRefreshingAllReceivers, value);
        }

        public bool SuppressErrorUpdates
        {
            get => _suppressErrorUpdates;
            private set => SetProperty(ref _suppressErrorUpdates, value);
        }

        public int LoadedPluginCount
        {
            get => _loadedPluginCount;
            private set
            {
                if (!SetProperty(ref _loadedPluginCount, value))
                    return;

                OnPropertyChanged(nameof(HasLoadedPlugins));
                OnPropertyChanged(nameof(PluginManagementVisibility));
            }
        }

        public bool HasLoadedPlugins => LoadedPluginCount > 0;

        public System.Windows.Visibility PluginManagementVisibility => HasLoadedPlugins ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string PendingChannel
        {
            get => _pendingChannel;
            set
            {
                string normalizedValue = new(value.Where(char.IsDigit).Take(3).ToArray());

                if (!SetProperty(ref _pendingChannel, normalizedValue))
                    return;

                ChannelStatusMessage = null;
                SetChannelCommand.NotifyCanExecuteChanged();
            }
        }

        public string? ChannelStatusMessage
        {
            get => _channelStatusMessage;
            private set => SetProperty(ref _channelStatusMessage, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (!SetProperty(ref _searchText, value))
                    return;

                _filteredReceivers.Refresh();
            }
        }

        public MainViewModel(IReceiverManager receiverManager, IReceiverCommandManager receiverCommandManager, ApplicationOverlayViewModel overlayViewModel)
        {
            _receiverManager = receiverManager;
            _receiverCommandManager = receiverCommandManager;
            _overlayViewModel = overlayViewModel;

            _receiverManager.SelectedReceiverChanged += SelectedReceiverChanged;
            _receiverCommandManager.ReceiverConnectionStatusChanged += ReceiverStatusChanged;
            _receiverCommandManager.ReceiverActivityStatusChanged += ReceiverStatusChanged;
            _channelProgressTimer.Tick += ChannelProgressTimerTick;
            _channelProgressTimer.Start();

            foreach (Receiver receiver in receiverManager.Receivers)
                _receivers.Add(new ReceiverViewModel(receiver));

            Receivers = new ReadOnlyObservableCollection<ReceiverViewModel>(_receivers);

            _filteredReceivers = CollectionViewSource.GetDefaultView(_receivers);
            _filteredReceivers.Filter = FilterReceiver;

            RefreshAllReceiversCommand = new AsyncRelayCommand(RefreshAllReceiversAsync, CanRefreshAllReceivers);
            RefreshSelectedReceiverCommand = new AsyncRelayCommand(RefreshSelectedReceiverAsync, CanRefreshSelectedReceiver, AsyncRelayCommandOptions.AllowConcurrentExecutions);
            SetChannelCommand = new AsyncRelayCommand(SetChannelAsync, CanSetChannel);

            AddReceiverCommand = new RelayCommand(() => AddReceiverRequested?.Invoke(this, EventArgs.Empty));
            ManageReceiversCommand = new RelayCommand(() => ManageReceiversRequested?.Invoke(this, EventArgs.Empty));
            VirtualRemoteCommand = new RelayCommand(() => VirtualRemoteRequested?.Invoke(SelectedReceiver!), () => SelectedReceiver?.CanExecuteActions == true);
            EditReceiverCommand = new RelayCommand(() => EditReceiverRequested?.Invoke(SelectedReceiver!), () => HasSelectedReceiver);
            ManagePluginsCommand = new RelayCommand(() => ManagePluginsRequested?.Invoke(this, EventArgs.Empty));
            SettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));

            if (receiverManager.Receivers is INotifyCollectionChanged collectionChanged)
                collectionChanged.CollectionChanged += Receivers_CollectionChanged;

            SynchronizeSelectedReceiver(_receiverManager.SelectedReceiver);
        }

        public void UpdateLoadedPluginCount(int loadedPluginCount)
        {
            LoadedPluginCount = Math.Max(0, loadedPluginCount);
        }

        private void SelectedReceiverChanged(object? sender, ReceiverSelectionChangedEventArgs e)
        {
            SynchronizeSelectedReceiver(e.SelectedReceiver);
        }

        private void SynchronizeSelectedReceiver(Receiver? receiver)
        {
            ReceiverViewModel? selectedReceiver = receiver is null ? null : _receivers.FirstOrDefault(item => ReferenceEquals(item.Model, receiver));

            if (!SetProperty(ref _selectedReceiver, selectedReceiver, nameof(SelectedReceiver)))
                return;

            OnPropertyChanged(nameof(HasSelectedReceiver));
            OnPropertyChanged(nameof(IsRefreshingReceiver));
            RefreshSelectedReceiverCommand.NotifyCanExecuteChanged();
            SetChannelCommand.NotifyCanExecuteChanged();
            VirtualRemoteCommand.NotifyCanExecuteChanged();
            EditReceiverCommand.NotifyCanExecuteChanged();
        }

        private async Task RefreshSelectedReceiverAsync(CancellationToken cancellationToken)
        {
            if (_receiverManager.SelectedReceiver is not { } receiver || !TryBeginReceiverRefresh(receiver))
                return;

            OnPropertyChanged(nameof(IsRefreshingReceiver));
            NotifyRefreshCommandsCanExecuteChanged();

            try
            {
                ShowInfo("Refreshing receiver", $"'{receiver.Configuration.Name}' status and channel information are being updated.");
                await _receiverCommandManager.RefreshReceiverAsync(receiver.Id, cancellationToken);

                if (receiver.LastError is null)
                    ShowSuccess("Receiver refreshed", $"{receiver.Configuration.Name} is up to date.");
            }
            finally
            {
                EndReceiverRefresh(receiver.Id);
                OnPropertyChanged(nameof(IsRefreshingReceiver));
                NotifyRefreshCommandsCanExecuteChanged();
                _ = ReleaseRefreshCooldownAsync(receiver.LastRefreshRequestedUtc?.Add(RefreshReceiverCooldown) ?? DateTimeOffset.UtcNow);
            }
        }

        private void ChannelProgressTimerTick(object? sender, EventArgs e)
        {
            SelectedReceiver?.RefreshChannelProgress();
        }

        private async Task RefreshAllReceiversAsync(CancellationToken cancellationToken)
        {
            if (!TryBeginRefreshAll())
                return;

            IsRefreshingAllReceivers = true;
            SuppressErrorUpdates = true;
            NotifyRefreshCommandsCanExecuteChanged();

            try
            {
                ShowInfo("Refreshing receivers", "Receiver status and channel information are being updated.");
                await _receiverCommandManager.RefreshAllReceiversAsync(cancellationToken);

                if (_receiverManager.Receivers.All(receiver => receiver.LastError is null))
                    ShowSuccess("Refresh complete", "All configured receivers have been checked.");
            }
            finally
            {
                SuppressErrorUpdates = false;
                IsRefreshingAllReceivers = false;
                NotifyRefreshCommandsCanExecuteChanged();
                _ = ReleaseRefreshCooldownAsync(_nextRefreshAllAtUtc);
            }
        }

        private bool CanRefreshSelectedReceiver()
        {
            lock (_refreshCooldownLock)
                return SelectedReceiver is { } receiver && !IsRefreshingAllReceivers && receiver.Model.ActivityStatus == ReceiverActivityStatus.Idle && !_refreshingReceiverIds.Contains(receiver.Id) && IsReceiverRefreshAvailable(receiver.Model, DateTimeOffset.UtcNow);
        }

        private bool CanRefreshAllReceivers()
        {
            lock (_refreshCooldownLock)
                return !IsRefreshingAllReceivers && DateTimeOffset.UtcNow >= _nextRefreshAllAtUtc;
        }

        private bool TryBeginReceiverRefresh(Receiver receiver)
        {
            lock (_refreshCooldownLock)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;

                if (_refreshingReceiverIds.Contains(receiver.Id) || !IsReceiverRefreshAvailable(receiver, now))
                    return false;

                receiver.MarkRefreshRequested(now);
                _refreshingReceiverIds.Add(receiver.Id);
                return true;
            }
        }

        private bool TryBeginRefreshAll()
        {
            lock (_refreshCooldownLock)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;

                if (IsRefreshingAllReceivers || now < _nextRefreshAllAtUtc)
                    return false;

                _nextRefreshAllAtUtc = now.Add(RefreshAllReceiversCooldown);
                return true;
            }
        }

        private bool IsReceiverRefreshing(Guid receiverId)
        {
            lock (_refreshCooldownLock)
                return _refreshingReceiverIds.Contains(receiverId);
        }

        private void EndReceiverRefresh(Guid receiverId)
        {
            lock (_refreshCooldownLock)
                _refreshingReceiverIds.Remove(receiverId);
        }

        private static bool IsReceiverRefreshAvailable(Receiver receiver, DateTimeOffset now)
        {
            return receiver.LastRefreshRequestedUtc is not { } lastRefreshRequestedUtc || now >= lastRefreshRequestedUtc.Add(RefreshReceiverCooldown);
        }

        private async Task ReleaseRefreshCooldownAsync(DateTimeOffset disabledUntilUtc)
        {
            while (DateTimeOffset.UtcNow < disabledUntilUtc)
                await Task.Delay(disabledUntilUtc - DateTimeOffset.UtcNow);

            NotifyRefreshCommandsCanExecuteChanged();
        }

        private void NotifyRefreshCommandsCanExecuteChanged()
        {
            RefreshSelectedReceiverCommand.NotifyCanExecuteChanged();
            RefreshAllReceiversCommand.NotifyCanExecuteChanged();
        }

        private bool CanSetChannel()
        {
            return SelectedReceiver?.CanSendChannelChange == true;
        }

        private async Task SetChannelAsync(CancellationToken cancellationToken)
        {
            if (_receiverManager.SelectedReceiver is not { } receiver)
                return;

            if (PendingChannel == string.Empty || PendingChannel == null)
                return;

            if (!TryNormalizeChannel(PendingChannel, out int channel))
            {
                ShowChannelValidationState(isValid: false);
                ChannelStatusMessage = "Enter a channel from 1 to 50.";
                PendingChannel = string.Empty;
                ShowError("Invalid channel", "Enter a channel from 1 to 50, or a normalized channel from 100 to 150.");
                return;
            }

            ShowChannelValidationState(isValid: true);

            FORCE_CH_SWITCH command = new();
            command.AddPayload(new CMD_PAYLOAD(serviceId: (ushort)channel));

            var result = await _receiverCommandManager.SendCommandAsync<JsonElement>(receiver.Id, command, cancellationToken: cancellationToken);

            if (result.IsSuccess)
                PendingChannel = string.Empty;

            ChannelStatusMessage = result.IsSuccess ? $"Channel {channel} command sent." : result.ErrorMessage;

            if (result.IsSuccess)
            {
                ShowSuccess("Channel updated", $"{receiver.Configuration.Name} was sent to channel {channel}.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    await _receiverCommandManager.RefreshReceiverEpgAsync(receiver.Id, cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    ShowError("EPG refresh unavailable", ex.Message);
                }
            }
            else
            {
                ShowError("Channel update failed", result.ErrorMessage ?? $"Unable to update {receiver.Configuration.Name}.");
            }
        }

        private static bool TryNormalizeChannel(string? input, out int channel)
        {
            channel = 0;

            if (!int.TryParse(input, out int parsed))
                return false;

            channel = parsed switch
            {
                >= 1 and <= 50 => parsed + 100,
                >= 100 and <= 150 => parsed,
                _ => 0
            };

            return channel != 0;
        }

        private void ShowChannelValidationState(bool isValid)
        {
            _channelValidationCancellationTokenSource?.Cancel();
            _channelValidationCancellationTokenSource?.Dispose();
            _channelValidationCancellationTokenSource = new CancellationTokenSource();

            IsChannelValid = isValid;
            IsChannelInvalid = !isValid;
            _ = ClearChannelValidationStateAsync(_channelValidationCancellationTokenSource.Token);
        }

        private async Task ClearChannelValidationStateAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(ChannelValidationDisplayDuration, cancellationToken);
                IsChannelValid = false;
                IsChannelInvalid = false;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ReceiverStatusChanged(object? sender, ReceiverChangedEventArgs e)
        {
            RefreshReceiver(e.Receiver);
            ShowReceiverError(e.Receiver);

            if (e.Receiver.Id == SelectedReceiver?.Id)
            {
                OnPropertyChanged(nameof(IsRefreshingReceiver));
                RefreshSelectedReceiverCommand.NotifyCanExecuteChanged();

                if (e.Receiver.ActivityStatus == ReceiverActivityStatus.Idle && e.Receiver.LastRefreshRequestedUtc is { } lastRefreshRequestedUtc)
                    _ = ReleaseRefreshCooldownAsync(lastRefreshRequestedUtc.Add(RefreshReceiverCooldown));
            }
        }

        public void ShowSuccess(string title, string message)
        {
            _overlayViewModel.ShowSuccess(title, message);
        }

        public void ShowInfo(string title, string message)
        {
            _overlayViewModel.ShowInformation(title, message);
        }

        public void ShowError(string title, string message)
        {
            _overlayViewModel.ShowError(title, message);
        }

        private void ShowReceiverError(Receiver receiver)
        {
            if (receiver.LastError is not { } error)
                return;

            if (SuppressErrorUpdates)
                return;

            string errorKey = $"{receiver.Id}|{error.OccurredAtUtc:O}|{error.ErrorCode}|{error.Message}|{error.InnerMessage}";

            if (!_displayedReceiverErrors.TryAdd(errorKey, 0))
                return;

            ShowError($"{receiver.Configuration.Name}: {error.Message}", string.IsNullOrWhiteSpace(error.InnerMessage) ? error.ErrorCode ?? "Receiver error" : error.InnerMessage);
        }

        public void RefreshReceiver(Receiver receiver)
        {
            ReceiverViewModel? receiverViewModel = _receivers.FirstOrDefault(item => item.Id == receiver.Id);
            receiverViewModel?.RefreshFromModel();

            if (receiverViewModel?.Id == SelectedReceiver?.Id)
            {
                SetChannelCommand.NotifyCanExecuteChanged();
                VirtualRemoteCommand.NotifyCanExecuteChanged();
            }
        }

        private bool FilterReceiver(object item)
        {
            if (item is not ReceiverViewModel receiver)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return receiver.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || receiver.ReceiverId.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || receiver.IpAddress.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void Receivers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddReceivers(e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveReceivers(e.OldItems);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveReceivers(e.OldItems);
                    AddReceivers(e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    MoveReceiver(e);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ResetReceivers();
                    break;
            }
        }

        private void AddReceivers(System.Collections.IList? items, int startingIndex)
        {
            if (items is null)
                return;

            int insertionIndex = startingIndex >= 0 ? startingIndex : _receivers.Count;

            foreach (object item in items)
            {
                if (item is Receiver receiver)
                    _receivers.Insert(insertionIndex++, new ReceiverViewModel(receiver));
            }
        }

        private void RemoveReceivers(System.Collections.IList? items)
        {
            if (items is null)
                return;

            foreach (object item in items)
            {
                if (item is not Receiver receiver)
                    continue;

                ReceiverViewModel? receiverViewModel = _receivers.FirstOrDefault(existing => existing.Id == receiver.Id);

                if (receiverViewModel is not null)
                    _receivers.Remove(receiverViewModel);
            }
        }

        private void MoveReceiver(NotifyCollectionChangedEventArgs e)
        {
            if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0)
                _receivers.Move(e.OldStartingIndex, e.NewStartingIndex);
        }

        private void ResetReceivers()
        {
            _receivers.Clear();

            foreach (Receiver receiver in _receiverManager.Receivers)
                _receivers.Add(new ReceiverViewModel(receiver));

            SynchronizeSelectedReceiver(_receiverManager.SelectedReceiver);
        }
    }
}
