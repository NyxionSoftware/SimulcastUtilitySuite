using SimulcastUtility.Plugin.Abstractions.Events;
using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace SimulcastUtility.Services
{
    public class ReceiverControllerService : IReceiverControllerService
    {
        private const int ReceiverPort = 25671;

        private readonly IReceiverConfigurationService _configurationService;
        private readonly ReceiverDiscoveryService _discoveryService;

        private readonly ObservableCollection<Receiver> _receivers = new();
        private readonly ReadOnlyObservableCollection<Receiver> _readOnlyReceivers;

        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        private Receiver? _selectedReceiver;
        private bool _isInitialized;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public event EventHandler? ReceiversInitialized;
        public event EventHandler? ReceiversReordered;

        public event EventHandler<ReceiverEventArgs>? ReceiverAdded;
        public event EventHandler<ReceiverEventArgs>? ReceiverRemoved;
        public event EventHandler<ReceiverUpdatedEventArgs>? ReceiverUpdated;
        public event EventHandler<ReceiverEventArgs>? ReceiverRefreshed;
        public event EventHandler<ReceiverEventArgs>? ReceiverEPGRefreshed;
        public event EventHandler<ReceiverEventArgs>? SelectedReceiverChanged;
        public event EventHandler<ReceiverStatusChangedEventArgs>? ReceiverStatusChanged;
        public event EventHandler<ReceiverCommandInvokedEventArgs>? ReceiverCommandInvoked;

        public ReadOnlyObservableCollection<Receiver> Receivers => _readOnlyReceivers;
        public bool IsInitialized => _isInitialized;
        public Receiver? SelectedReceiver
        {
            get => _selectedReceiver;

            set
            {
                if (ReferenceEquals(_selectedReceiver, value))
                    return;

                if (value is not null && !_receivers.Contains(value))
                {
                    throw new InvalidOperationException("The selected receiver must belong to the controller's receiver collection.");
                }

                _selectedReceiver = value;
                SelectedReceiverChanged?.Invoke(this, new ReceiverEventArgs(value));
            }
        }

        public ReceiverControllerService(IReceiverConfigurationService configurationService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            _discoveryService = new ReceiverDiscoveryService();

            _readOnlyReceivers = new ReadOnlyObservableCollection<Receiver>(_receivers);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return;

            await _initializationLock.WaitAsync(cancellationToken);

            try
            {
                if (_isInitialized)
                    return;

                ObservableCollection<Receiver> loadedReceivers = await _configurationService.LoadReceiversAsync(cancellationToken);

                await RunOnDispatcherAsync(() =>
                {
                    _receivers.Clear();

                    foreach (Receiver receiver in loadedReceivers)
                    {
                        _receivers.Add(receiver);
                    }

                    SelectedReceiver = _receivers.FirstOrDefault();
                });

                foreach (var receiver in loadedReceivers)
                {
                    await _discoveryService.DiscoverAsync(receiver, cancellationToken);
                }

                _isInitialized = true;

                ReceiversInitialized?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        private async Task ObserveEpgLoadAsync(Receiver receiver, Task epgLoadTask)
        {
            try
            {
                await epgLoadTask;
                ReceiverEPGRefreshed?.Invoke(this, new ReceiverEventArgs(receiver));
            }
            catch (Exception ex)
            {
                receiver.LastError = $"Unable to retrieve EPG information: {ex.Message}";
            }
        }

        public async Task<ReceiverOperationResult> SaveReceiverOrderAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                await SaveAsync(cancellationToken);

                ReceiversReordered?.Invoke(this, EventArgs.Empty);

                return ReceiverOperationResult.Success(changed: true);
            }
            catch (OperationCanceledException)
            {
                return ReceiverOperationResult.Failure("Saving the receiver order was cancelled.");
            }
            catch (Exception ex)
            {
                return ReceiverOperationResult.Failure($"Failed to save receiver order: {ex.Message}");
            }
        }

        public Task<ReceiverOperationResult> MoveReceiverAsync(Receiver draggedReceiver, Receiver targetReceiver, bool insertAfter = true, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(draggedReceiver);
            ArgumentNullException.ThrowIfNull(targetReceiver);

            if (!_receivers.Contains(draggedReceiver) || !_receivers.Contains(targetReceiver))
            {
                return Task.FromResult(ReceiverOperationResult.Failure("Both receivers must belong to the controller."));
            }

            int targetIndex = _receivers.IndexOf(targetReceiver);

            if (insertAfter)
                targetIndex++;

            return MoveReceiverToIndexAsync(draggedReceiver, targetIndex, saveChanges: true, cancellationToken);
        }

        public async Task<ReceiverOperationResult> MoveReceiverToIndexAsync(Receiver receiver, int targetIndex, bool saveChanges = true, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            cancellationToken.ThrowIfCancellationRequested();

            if (!_receivers.Contains(receiver))
            {
                return ReceiverOperationResult.Failure("The receiver does not belong to this controller.");
            }

            if (_receivers.Count <= 1)
                return ReceiverOperationResult.Success(changed: false);

            try
            {
                bool changed = false;

                await RunOnDispatcherAsync(() =>
                {
                    int oldIndex =
                        _receivers.IndexOf(receiver);

                    if (oldIndex < 0)
                        return;

                    // Permit Count as an insertion index representing the end
                    // of the collection. This keeps plugin callers safe.
                    int safeInsertionIndex = Math.Clamp(targetIndex, 0, _receivers.Count);

                    // Removing an item before the insertion point shifts that
                    // insertion point left by one.
                    if (oldIndex < safeInsertionIndex)
                        safeInsertionIndex--;

                    int safeFinalIndex = Math.Clamp(safeInsertionIndex, 0, _receivers.Count - 1);

                    if (oldIndex == safeFinalIndex)
                        return;

                    _receivers.Move(oldIndex, safeFinalIndex);

                    changed = true;
                });

                if (!changed)
                    return ReceiverOperationResult.Success(changed: false);

                if (saveChanges)
                {
                    await SaveAsync(cancellationToken);

                    ReceiversReordered?.Invoke(this, EventArgs.Empty);
                }

                return ReceiverOperationResult.Success(changed: true);
            }
            catch (OperationCanceledException)
            {
                return ReceiverOperationResult.Failure("The receiver move was cancelled.");
            }
            catch (Exception ex)
            {
                return ReceiverOperationResult.Failure($"Failed to move receiver: {ex.Message}");
            }
        }

        public async Task<ReceiverOperationResult> AddReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            string? validationError = ValidateReceiver(receiver);

            if (validationError is not null)
            {
                return ReceiverOperationResult.Failure(validationError);
            }

            bool duplicateExists = _receivers.Any(existing =>
                string.Equals(existing.ReceiverId, receiver.ReceiverId, StringComparison.Ordinal) ||
                string.Equals(existing.IpAddress, receiver.IpAddress, StringComparison.OrdinalIgnoreCase));

            if (duplicateExists)
            {
                return ReceiverOperationResult.Failure("A receiver with the same receiver ID or IP address already exists.");
            }

            var result = await _discoveryService.DiscoverAsync(receiver, cancellationToken);
            var discoveryResult = result.DiscoveryResult;
            _ = ObserveEpgLoadAsync(receiver, result.EpgLoadTask);

            if (!discoveryResult.IsSuccess)
            {
                return ReceiverOperationResult.Failure("Invalid IP Address or Receiver ID. This receiver was unable to be added.");
            }

            await RunOnDispatcherAsync(() =>
            {
                _receivers.Add(receiver);

                SelectedReceiver ??= receiver;
            });

            try
            {
                await SaveAsync(cancellationToken);
            }
            catch
            {
                await RunOnDispatcherAsync(() =>
                {
                    _receivers.Remove(receiver);

                    if (ReferenceEquals(SelectedReceiver, receiver))
                    {
                        SelectedReceiver = _receivers.FirstOrDefault();
                    }
                });

                throw;
            }

            ReceiverAdded?.Invoke(this, new ReceiverEventArgs(receiver));

            return ReceiverOperationResult.Success();
        }

        public async Task<ReceiverOperationResult> UpdateReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            if (!_receivers.Contains(receiver))
            {
                return ReceiverOperationResult.Failure("The receiver does not belong to this controller.");
            }

            string? validationError = ValidateReceiver(receiver);

            if (validationError is not null)
            {
                return ReceiverOperationResult.Failure(validationError);
            }

            bool duplicateExists = _receivers.Any(existing =>
                !ReferenceEquals(existing, receiver) &&
                (string.Equals(existing.ReceiverId, receiver.ReceiverId, StringComparison.Ordinal) ||
                 string.Equals(existing.IpAddress, receiver.IpAddress, StringComparison.OrdinalIgnoreCase)
                ));

            if (duplicateExists)
            {
                return ReceiverOperationResult.Failure("Another receiver already uses this receiver ID or IP address.");
            }

            await SaveAsync(cancellationToken);

            ReceiverUpdated?.Invoke(this, new ReceiverUpdatedEventArgs(receiver));

            return ReceiverOperationResult.Success();
        }

        public async Task<ReceiverOperationResult> RemoveReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            if (!_receivers.Contains(receiver))
            {
                return ReceiverOperationResult.Failure("The receiver does not belong to this controller.");
            }

            int previousIndex = _receivers.IndexOf(receiver);

            bool wasSelected = ReferenceEquals(SelectedReceiver, receiver);

            await RunOnDispatcherAsync(() =>
            {
                _receivers.Remove(receiver);

                if (wasSelected)
                {
                    SelectedReceiver = _receivers.FirstOrDefault();
                }
            });

            try
            {
                await SaveAsync(cancellationToken);
            }
            catch
            {
                await RunOnDispatcherAsync(() =>
                {
                    int insertIndex = Math.Clamp(previousIndex, 0, _receivers.Count);

                    _receivers.Insert(insertIndex, receiver);

                    if (wasSelected)
                    {
                        SelectedReceiver = receiver;
                    }
                });

                throw;
            }

            ReceiverRemoved?.Invoke(this, new ReceiverEventArgs(receiver));

            return ReceiverOperationResult.Success();
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            await _saveLock.WaitAsync(cancellationToken);

            try
            {
                List<Receiver> snapshot = await GetReceiverSnapshotAsync();

                await _configurationService.SaveReceiversAsync(snapshot, cancellationToken);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        public async Task RefreshReceiverAsync(Receiver receiver, RefreshBehavior refreshBehavior, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            if (!_receivers.Contains(receiver))
            {
                throw new InvalidOperationException("The receiver does not belong to this controller.");
            }

            try
            {
                if (!receiver.CanRefresh && refreshBehavior == RefreshBehavior.Immediate)
                {
                    TimeSpan elapsed = DateTime.UtcNow - receiver.LastRefreshUtc;
                    TimeSpan remaining = TimeSpan.FromSeconds(15) - elapsed;
                    throw new InvalidOperationException($"Please wait {remaining.TotalSeconds:F0} more second{(remaining.TotalSeconds >= 1.5 ? "s" : "")} before refreshing this receiver again.");
                }

                if (refreshBehavior == RefreshBehavior.WaitForRefreshWindow)
                {
                    TimeSpan remaining = receiver.LastRefreshUtc - DateTime.UtcNow;

                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, cancellationToken);
                    }
                }

                receiver.LastRefreshUtc = DateTime.UtcNow;

                await SetReceiverStatusAsync(receiver, ReceiverStatus.Loading);

                var result = await _discoveryService.DiscoverAsync(receiver, cancellationToken);
                var discoveryResult = result.DiscoveryResult;
                _ = ObserveEpgLoadAsync(receiver, result.EpgLoadTask);

                if (discoveryResult.IsSuccess)
                {
                    await SetReceiverStatusAsync(receiver, ReceiverStatus.Online);

                    ReceiverUpdated?.Invoke(this, new ReceiverUpdatedEventArgs(receiver));
                    return;
                }

                receiver.LastError = discoveryResult.ErrorMessage;

                await SetReceiverStatusAsync(receiver, ReceiverStatus.Offline);

                ReceiverUpdated?.Invoke(this, new ReceiverUpdatedEventArgs(receiver));
            }
            catch (OperationCanceledException)
            {
                await SetReceiverStatusAsync(receiver, ReceiverStatus.Offline);
                throw;
            }
            catch (InvalidOperationException)
            {
                await SetReceiverStatusAsync(receiver, ReceiverStatus.Offline);
                throw;
            }
            catch (Exception ex)
            {
                receiver.LastError = ex.Message;

                await SetReceiverStatusAsync(receiver, ReceiverStatus.Offline);
                return;
            }
            finally
            {
                ReceiverRefreshed?.Invoke(this, new ReceiverEventArgs(receiver));
            }
        }

        private readonly SemaphoreSlim _refreshAllSemaphore = new(1, 1);
        public async Task RefreshAllReceiversAsync(CancellationToken cancellationToken = default)
        {
            if (!await _refreshAllSemaphore.WaitAsync(0, cancellationToken))
                return;

            try
            {
                Receiver[] receivers = (await GetReceiverSnapshotAsync()).ToArray();

                IEnumerable<Task> refreshTasks = receivers.Select(receiver => RefreshReceiverAsync(receiver, RefreshBehavior.WaitForRefreshWindow, cancellationToken));

                await Task.WhenAll(refreshTasks);
            }
            finally
            {
                _refreshAllSemaphore.Release();
            }
        }

        public Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(Receiver receiver, CMD_STB_MESSAGE command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            return SendReceiverCommandAsync<TResponse>(receiver, command, timeout, cancellationToken);
        }

        public async Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(string receiverIpAddress, string receiverId, CMD_STB_MESSAGE command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            CommandResult<TResponse> result = await SendLegacyCommandAsync<TResponse>(receiverIpAddress, receiverId, command, timeout, cancellationToken);

            ReceiverCommandInvoked?.Invoke(this, new ReceiverCommandInvokedEventArgs(receiverId, command, result.IsSuccess, result.ErrorMessage));

            return result;
        }

        private async Task<CommandResult<TResponse>> SendReceiverCommandAsync<TResponse>(Receiver receiver, CMD_STB_MESSAGE command, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            await receiver.CommandLock.WaitAsync(cancellationToken);

            try
            {
                CommandResult<TResponse> result = await SendLegacyCommandAsync<TResponse>(receiver.IpAddress, receiver.ReceiverId, command, timeout, cancellationToken);

                if (result.IsSuccess)
                {
                    await SetReceiverStatusAsync(receiver, ReceiverStatus.Online);
                }
                else
                {
                    receiver.LastError = result.ErrorMessage;

                    await SetReceiverStatusAsync(receiver, ReceiverStatus.Offline);
                }

                ReceiverCommandInvoked?.Invoke(this, new ReceiverCommandInvokedEventArgs(receiver.ReceiverId, command, result.IsSuccess, result.ErrorMessage));

                return result;
            }
            finally
            {
                receiver.CommandLock.Release();
            }
        }

        private async Task<CommandResult<TResponse>> SendLegacyCommandAsync<TResponse>(string receiverIpAddress, string receiverId, CMD_STB_MESSAGE command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            timeout ??= TimeSpan.FromSeconds(5);

            if (!IPAddress.TryParse(receiverIpAddress, out IPAddress? ipAddress))
            {
                return CommandResult<TResponse>.Failure($"'{receiverIpAddress}' is not a valid IP address.");
            }

            if (!uint.TryParse(receiverId, out uint parsedReceiverId))
            {
                return CommandResult<TResponse>.Failure($"'{receiverId}' is not a valid receiver ID.");
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeoutSource.CancelAfter(timeout.Value);

            try
            {
                string commandJson = JsonSerializer.Serialize(command, SerializerOptions);

                string requestText = $"{parsedReceiverId}:msg: {commandJson}";

                byte[] requestBytes = Encoding.ASCII.GetBytes(requestText);

                using var udpClient = new UdpClient();

                udpClient.Connect(new IPEndPoint(ipAddress, ReceiverPort));

                await udpClient.SendAsync(requestBytes, timeoutSource.Token);

                while (true)
                {
                    UdpReceiveResult received = await udpClient.ReceiveAsync(timeoutSource.Token);

                    string rawResponse = Encoding.ASCII.GetString(received.Buffer);

                    if (!TryGetResponseCommandId(rawResponse, out int responseCommandId))
                    {
                        if (TryDeserializeResponse(rawResponse, out TResponse? directResponse))
                        {
                            return CommandResult<TResponse>.Success(directResponse);
                        }

                        continue;
                    }

                    if (responseCommandId != command.Id)
                        continue;

                    if (!TryDeserializeResponse(rawResponse, out TResponse? response))
                    {
                        return CommandResult<TResponse>.Failure("The receiver responded, but the response could not be parsed.");
                    }

                    return CommandResult<TResponse>.Success(response);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CommandResult<TResponse>.Failure($"The receiver did not respond within {timeout.Value.TotalSeconds:0.#} seconds.");
            }
            catch (OperationCanceledException)
            {
                return CommandResult<TResponse>.Failure("The receiver command was cancelled.");
            }
            catch (SocketException ex)
            {
                return CommandResult<TResponse>.Failure($"A socket error occurred: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                return CommandResult<TResponse>.Failure($"The receiver returned invalid JSON: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                return CommandResult<TResponse>.Failure($"An unexpected command error occurred: {ex.Message}", ex);
            }
        }

        private async Task SetReceiverStatusAsync(Receiver receiver, ReceiverStatus status)
        {
            await RunOnDispatcherAsync(() =>
            {
                receiver.Status = status;
            });

            ReceiverStatusChanged?.Invoke(this, new ReceiverStatusChangedEventArgs(receiver, status));
        }

        private async Task<List<Receiver>> GetReceiverSnapshotAsync()
        {
            List<Receiver>? snapshot = null;

            await RunOnDispatcherAsync(() =>
            {
                snapshot = _receivers.ToList();
            });

            return snapshot!;
        }

        private static string? ValidateReceiver(Receiver receiver)
        {
            if (string.IsNullOrWhiteSpace(receiver.Name))
                return "The receiver name is required.";

            if (string.IsNullOrWhiteSpace(receiver.ReceiverId))
                return "The receiver ID is required.";

            if (!uint.TryParse(receiver.ReceiverId, out _))
                return "The receiver ID must be numeric.";

            if (!IPAddress.TryParse(receiver.IpAddress, out _))
                return "The receiver IP address is invalid.";

            return null;
        }

        private static Task RunOnDispatcherAsync(Action action)
        {
            Application? application = Application.Current;

            if (application is null || application.Dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return application.Dispatcher.InvokeAsync(action).Task;
        }

        private static bool TryGetResponseCommandId(string rawResponse, out int commandId)
        {
            commandId = default;

            foreach (string json in ExtractJsonObjects(rawResponse))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);

                    JsonElement root = document.RootElement;

                    if (!root.TryGetProperty("id", out JsonElement idElement))
                    {
                        continue;
                    }

                    if (idElement.ValueKind == JsonValueKind.Number &&
                        idElement.TryGetInt32(out commandId))
                    {
                        return true;
                    }

                    if (idElement.ValueKind == JsonValueKind.String &&
                        int.TryParse(idElement.GetString(), out commandId))
                    {
                        return true;
                    }
                }
                catch (JsonException)
                {
                    // This extracted block was not usable JSON, so safely catch the ex :)
                }
            }

            return false;
        }

        private static bool TryDeserializeResponse<TResponse>(string rawResponse, out TResponse? response)
        {
            response = default;

            IReadOnlyList<string> jsonObjects = ExtractJsonObjects(rawResponse);

            foreach (string json in jsonObjects.Reverse())
            {
                try
                {
                    TResponse? parsed = JsonSerializer.Deserialize<TResponse>(json, SerializerOptions);

                    if (parsed is not null)
                    {
                        response = parsed;
                        return true;
                    }
                }
                catch (JsonException)
                {
                    // Try the next JSON object.
                }
            }

            if (!IsCommandResponseWrapper(typeof(TResponse)))
            {
                foreach (string json in jsonObjects)
                {
                    if (TryDeserializeDetails(json, out response))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsCommandResponseWrapper(Type responseType)
        {
            return responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(CMD_STB_MESSAGE_RESPONSE<>);
        }

        private static bool TryDeserializeDetails<TResponse>(string json, out TResponse? response)
        {
            response = default;

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                JsonElement root = document.RootElement;

                if (!root.TryGetProperty("details", out JsonElement detailsElement))
                {
                    return false;
                }

                TResponse? parsed = detailsElement.Deserialize<TResponse>(SerializerOptions);

                if (parsed is null)
                    return false;

                response = parsed;

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static IReadOnlyList<string> ExtractJsonObjects(string input)
        {
            var results = new List<string>();

            int depth = 0;
            int startIndex = -1;
            bool insideString = false;
            bool escaped = false;

            for (int index = 0; index < input.Length; index++)
            {
                char character = input[index];

                if (insideString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (character == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (character == '"')
                        insideString = false;

                    continue;
                }

                if (character == '"')
                {
                    insideString = true;
                    continue;
                }

                if (character == '{')
                {
                    if (depth == 0)
                        startIndex = index;

                    depth++;
                    continue;
                }

                if (character != '}')
                    continue;

                if (depth == 0)
                    continue;

                depth--;

                if (depth == 0 && startIndex >= 0)
                {
                    results.Add(input.Substring(startIndex, index - startIndex + 1));

                    startIndex = -1;
                }
            }

            return results;
        }
    }
}
