using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Protocol;
using SimulcastUtility.Application.Protocol.Responses;
using SimulcastUtility.Application.Requests;
using SimulcastUtility.Core.Models;
using SimulcastUtility.Wpf.ViewModels.Models;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class ReceiverDiscoveryViewModel : ObservableObject, IDisposable
    {
        private readonly IReceiverManager _receiverManager;
        private readonly IReceiverCommandManager _receiverCommandManager;
        private readonly MainViewModel _notificationHost;
        private CancellationTokenSource? _discoveryCancellationTokenSource;
        private string _startIpAddress = string.Empty;
        private string _endIpAddress = string.Empty;
        private string _timeoutMilliseconds = "750";
        private string _validationMessage = string.Empty;
        private string _progressText = string.Empty;
        private int _progressValue;
        private int _progressMaximum = 1;
        private bool _isDiscovering;
        private bool _isSaving;

        public ObservableCollection<DiscoveredReceiverViewModel> DiscoveredReceivers { get; } = new();

        public string StartIpAddress
        {
            get => _startIpAddress;
            set
            {
                if (SetProperty(ref _startIpAddress, value))
                    ValidateInputs();
            }
        }

        public string EndIpAddress
        {
            get => _endIpAddress;
            set
            {
                if (SetProperty(ref _endIpAddress, value))
                    ValidateInputs();
            }
        }

        public string TimeoutMilliseconds
        {
            get => _timeoutMilliseconds;
            set
            {
                if (SetProperty(ref _timeoutMilliseconds, value))
                    ValidateInputs();
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (SetProperty(ref _validationMessage, value))
                    OnPropertyChanged(nameof(ValidationVisibility));
            }
        }

        public Visibility ValidationVisibility => string.IsNullOrWhiteSpace(ValidationMessage) ? Visibility.Collapsed : Visibility.Visible;

        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }

        public int ProgressValue
        {
            get => _progressValue;
            private set => SetProperty(ref _progressValue, value);
        }

        public int ProgressMaximum
        {
            get => _progressMaximum;
            private set => SetProperty(ref _progressMaximum, value);
        }

        public bool IsDiscovering
        {
            get => _isDiscovering;
            private set
            {
                if (!SetProperty(ref _isDiscovering, value))
                    return;

                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(ProgressVisibility));
                NotifyCommandsCanExecuteChanged();
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (!SetProperty(ref _isSaving, value))
                    return;

                OnPropertyChanged(nameof(IsBusy));
                NotifyCommandsCanExecuteChanged();
            }
        }

        public bool IsBusy => IsDiscovering || IsSaving;

        public Visibility ProgressVisibility => IsDiscovering ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ResultsVisibility => DiscoveredReceivers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyResultsVisibility => DiscoveredReceivers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public IAsyncRelayCommand DiscoverCommand { get; }

        public IRelayCommand CancelDiscoveryCommand { get; }

        public IAsyncRelayCommand SaveSelectedCommand { get; }

        public IRelayCommand BackCommand { get; }

        public event EventHandler? BackRequested;

        public ReceiverDiscoveryViewModel(IReceiverManager receiverManager, IReceiverCommandManager receiverCommandManager, MainViewModel notificationHost)
        {
            _receiverManager = receiverManager;
            _receiverCommandManager = receiverCommandManager;
            _notificationHost = notificationHost;
            DiscoverCommand = new AsyncRelayCommand(DiscoverAsync, CanDiscover);
            CancelDiscoveryCommand = new RelayCommand(() => _discoveryCancellationTokenSource?.Cancel(), () => IsDiscovering);
            SaveSelectedCommand = new AsyncRelayCommand(SaveSelectedAsync, CanSaveSelected);
            BackCommand = new RelayCommand(Back, () => !IsSaving);
            SetDefaultRange();
            ValidateInputs();
        }

        private void SetDefaultRange()
        {
            string? configuredAddress = _receiverManager.Receivers.Select(receiver => receiver.Configuration.IpAddress).FirstOrDefault(address => TryParseIpv4(address, out _));

            if (!TryParseIpv4(configuredAddress, out byte[]? octets))
                return;

            StartIpAddress = $"{octets[0]}.{octets[1]}.{octets[2]}.1";
            EndIpAddress = $"{octets[0]}.{octets[1]}.{octets[2]}.254";
        }

        private async Task DiscoverAsync(CancellationToken commandCancellationToken)
        {
            if (!TryGetRange(out IReadOnlyList<string> addresses, out int timeoutMilliseconds, out string error))
            {
                ValidationMessage = error;
                return;
            }

            foreach (DiscoveredReceiverViewModel receiver in DiscoveredReceivers)
                receiver.PropertyChanged -= DiscoveredReceiverPropertyChanged;

            DiscoveredReceivers.Clear();
            OnPropertyChanged(nameof(ResultsVisibility));
            OnPropertyChanged(nameof(EmptyResultsVisibility));
            ConcurrentBag<(string IpAddress, string ReceiverId)> discovered = new();
            ProgressMaximum = addresses.Count;
            ProgressValue = 0;
            ProgressText = $"Scanning 0 of {addresses.Count} addresses...";
            _discoveryCancellationTokenSource?.Dispose();
            _discoveryCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(commandCancellationToken);
            CancellationToken cancellationToken = _discoveryCancellationTokenSource.Token;
            IsDiscovering = true;

            try
            {
                using SemaphoreSlim concurrency = new(32);
                int completed = 0;
                IEnumerable<Task> probes = addresses.Select(async address =>
                {
                    bool entered = false;

                    try
                    {
                        await concurrency.WaitAsync(cancellationToken);
                        entered = true;
                        CommandResult<HELLO_DISCOVERY_RESPONSE> result = await _receiverCommandManager.DiscoverReceiverAtIpAsync(address, TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken);

                        if (result.IsSuccess && result.Response is { } response && uint.TryParse(response.StbChipID, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                            discovered.Add((address, response.StbChipID));
                    }
                    finally
                    {
                        if (entered)
                            concurrency.Release();

                        int current = Interlocked.Increment(ref completed);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressValue = current;
                            ProgressText = $"Scanning {current} of {addresses.Count} addresses...";
                        });
                    }
                });

                await Task.WhenAll(probes);

                foreach ((string ipAddress, string receiverId) in discovered.Distinct().OrderBy(item => IPAddress.Parse(item.IpAddress).GetAddressBytes()[3]))
                {
                    bool configured = _receiverManager.Receivers.Any(receiver => string.Equals(receiver.Configuration.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase) || string.Equals(receiver.Configuration.ReceiverId, receiverId, StringComparison.Ordinal));
                    DiscoveredReceiverViewModel item = new(ipAddress, receiverId, configured);
                    item.PropertyChanged += DiscoveredReceiverPropertyChanged;
                    DiscoveredReceivers.Add(item);
                }

                RecalculateNameAvailability();

                OnPropertyChanged(nameof(ResultsVisibility));
                OnPropertyChanged(nameof(EmptyResultsVisibility));
                SaveSelectedCommand.NotifyCanExecuteChanged();

                if (DiscoveredReceivers.Count == 0)
                    _notificationHost.ShowInfo("Discovery complete", "No receivers responded in the selected IP range.");
                else
                    _notificationHost.ShowSuccess("Discovery complete", $"Found {DiscoveredReceivers.Count} receiver{(DiscoveredReceivers.Count == 1 ? string.Empty : "s")}.");
            }
            catch (OperationCanceledException)
            {
                _notificationHost.ShowInfo("Discovery cancelled", "The receiver scan was cancelled.");
            }
            finally
            {
                IsDiscovering = false;
            }
        }

        private async Task SaveSelectedAsync(CancellationToken cancellationToken)
        {
            List<DiscoveredReceiverViewModel> selected = DiscoveredReceivers.Where(receiver => receiver.CanSave).ToList();

            if (selected.Count == 0)
                return;

            IsSaving = true;
            int savedCount = 0;

            try
            {
                foreach (DiscoveredReceiverViewModel item in selected)
                {
                    try
                    {
                        Receiver receiver = await _receiverManager.AddReceiverAsync(new ReceiverCreateRequest(item.DisplayName.Trim(), item.ReceiverId, item.IpAddress), cancellationToken);
                        item.IsSaved = true;
                        savedCount++;
                        await _receiverCommandManager.RefreshReceiverAsync(receiver.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _notificationHost.ShowError($"Could not save {item.IpAddress}", ex.Message);
                    }
                }

                if (savedCount > 0)
                    _notificationHost.ShowSuccess("Receivers saved", $"Added {savedCount} receiver{(savedCount == 1 ? string.Empty : "s")}.");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private bool CanDiscover()
        {
            return !IsBusy && TryGetRange(out _, out _, out _);
        }

        private bool CanSaveSelected()
        {
            return !IsBusy && DiscoveredReceivers.Any(receiver => receiver.CanSave);
        }

        private void ValidateInputs()
        {
            ValidationMessage = TryGetRange(out _, out _, out string error) ? string.Empty : error;
            DiscoverCommand?.NotifyCanExecuteChanged();
        }

        private bool TryGetRange(out IReadOnlyList<string> addresses, out int timeoutMilliseconds, out string error)
        {
            addresses = Array.Empty<string>();
            timeoutMilliseconds = 0;

            if (!TryParseIpv4(StartIpAddress, out byte[]? start) || !TryParseIpv4(EndIpAddress, out byte[]? end))
            {
                error = "Enter two valid IPv4 addresses.";
                return false;
            }

            if (!start.Take(3).SequenceEqual(end.Take(3)))
            {
                error = "The first three octets must match for both addresses.";
                return false;
            }

            if (start[3] > end[3])
            {
                error = "The starting address must come before the ending address.";
                return false;
            }

            if (!int.TryParse(TimeoutMilliseconds, NumberStyles.None, CultureInfo.InvariantCulture, out timeoutMilliseconds) || timeoutMilliseconds is < 100 or > 10000)
            {
                error = "Timeout must be between 100 and 10,000 milliseconds.";
                return false;
            }

            string prefix = $"{start[0]}.{start[1]}.{start[2]}";
            addresses = Enumerable.Range(start[3], end[3] - start[3] + 1).Select(lastOctet => $"{prefix}.{lastOctet}").ToArray();
            error = string.Empty;
            return true;
        }

        private static bool TryParseIpv4(string? value, out byte[] octets)
        {
            octets = Array.Empty<byte>();

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] parts = value.Split('.');

            if (parts.Length != 4 || parts.Any(part => part.Length == 0 || !byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                return false;

            if (!IPAddress.TryParse(value, out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            octets = parts.Select(part => byte.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            return true;
        }

        private void DiscoveredReceiverPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DiscoveredReceiverViewModel.DisplayName) or nameof(DiscoveredReceiverViewModel.IsSelected) or nameof(DiscoveredReceiverViewModel.IsSaved))
                RecalculateNameAvailability();

            SaveSelectedCommand.NotifyCanExecuteChanged();
        }

        private void RecalculateNameAvailability()
        {
            foreach (DiscoveredReceiverViewModel receiver in DiscoveredReceivers)
            {
                if (receiver.IsAlreadyConfigured || receiver.IsSaved)
                {
                    receiver.IsNameInUse = false;
                    continue;
                }

                string name = receiver.DisplayName.Trim();
                bool configuredNameExists = name.Length > 0 && _receiverManager.Receivers.Any(configured => string.Equals(configured.Configuration.Name, name, StringComparison.OrdinalIgnoreCase));
                bool discoveredNameExists = receiver.IsSelected && name.Length > 0 && DiscoveredReceivers.Any(other => !ReferenceEquals(other, receiver) && other.IsSelected && !other.IsSaved && string.Equals(other.DisplayName.Trim(), name, StringComparison.OrdinalIgnoreCase));
                receiver.IsNameInUse = configuredNameExists || discoveredNameExists;
            }
        }

        private void NotifyCommandsCanExecuteChanged()
        {
            DiscoverCommand.NotifyCanExecuteChanged();
            CancelDiscoveryCommand.NotifyCanExecuteChanged();
            SaveSelectedCommand.NotifyCanExecuteChanged();
            BackCommand.NotifyCanExecuteChanged();
        }

        private void Back()
        {
            _discoveryCancellationTokenSource?.Cancel();
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _discoveryCancellationTokenSource?.Cancel();
            _discoveryCancellationTokenSource?.Dispose();

            foreach (DiscoveredReceiverViewModel receiver in DiscoveredReceivers)
                receiver.PropertyChanged -= DiscoveredReceiverPropertyChanged;
        }
    }
}
