using Microsoft.Extensions.Logging;
using SimulcastUtility.Application.Events;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Protocol;
using SimulcastUtility.Application.Protocol.Commands;
using SimulcastUtility.Application.Protocol.Details;
using SimulcastUtility.Application.Protocol.Payloads;
using SimulcastUtility.Application.Protocol.Responses;
using SimulcastUtility.Core.Enums;
using SimulcastUtility.Core.Exceptions;
using SimulcastUtility.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

namespace SimulcastUtility.Application.Services
{
    public class ReceiverCommandManager : IReceiverCommandManager
    {
        private readonly IReceiverManager _receiverManager;
        private readonly ILogger<ReceiverCommandManager> _logger;
        private readonly ConcurrentDictionary<string, ReceiverCommandThrottleState> _commandThrottleStates = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, int> _activeReceiverCommandCounts = new();
        private readonly object _receiverActivityLock = new();
        private const int ReceiverPort = 25671;
        private static readonly TimeSpan MinimumCommandInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ReceiverRefreshCooldown = TimeSpan.FromSeconds(5);
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public event EventHandler<ReceiverChangedEventArgs>? ReceiverConnectionStatusChanged;
        public event EventHandler<ReceiverChangedEventArgs>? ReceiverActivityStatusChanged;

        public ReceiverCommandManager(IReceiverManager receiverManager, ILogger<ReceiverCommandManager> logger)
        {
            _receiverManager = receiverManager;
            _logger = logger;
        }

        public async Task RefreshReceiverAsync(Guid receiverId, CancellationToken cancellationToken = default)
        {
            Receiver receiver = _receiverManager.GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);
            receiver.MarkRefreshRequested(DateTimeOffset.UtcNow);
            var result = await DiscoverReceiverAsync(receiverId, cancellationToken);
            await InvokeReceiverEPG(receiverId, result.EpgLoadTask);
        }

        public async Task RefreshAllReceiversAsync(CancellationToken cancellationToken = default)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var receivers = _receiverManager.Receivers.Where(receiver => receiver.ActivityStatus == ReceiverActivityStatus.Idle && (receiver.LastRefreshRequestedUtc is not { } lastRefreshRequestedUtc || now >= lastRefreshRequestedUtc.Add(ReceiverRefreshCooldown))).ToList();
            Task[] refreshTasks = receivers.Select(receiver => RefreshReceiverAsync(receiver.Id, cancellationToken)).ToArray();
            await Task.WhenAll(refreshTasks);
        }

        public async Task RefreshReceiverEpgAsync(Guid receiverId, CancellationToken cancellationToken = default)
        {
            Receiver receiver = _receiverManager.GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);

            if (receiver.ConnectionStatus != ReceiverConnectionStatus.Online)
                throw new InvalidOperationException($"Receiver '{receiver.Configuration.Name}' must be online before refreshing EPG information.");

            try
            {
                await DiscoverReceiverEPG(receiverId, cancellationToken);
            }
            finally
            {
                if (receiver.ActivityStatus != ReceiverActivityStatus.Idle)
                {
                    receiver.SetActivityStatus(ReceiverActivityStatus.Idle);
                    ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));
                }
            }
        }

        public async Task VerifyReceiverAsync(string receiverIpAddress, string receiverId, CancellationToken cancellationToken = default)
        {
            CommandResult<HELLO_DISCOVERY_RESPONSE> result = await SendCommandAsync<HELLO_DISCOVERY_RESPONSE>(receiverIpAddress, receiverId, HELLO_DISCOVERY.Default, TimeSpan.FromSeconds(5), cancellationToken, ReceiverCommandExecutionOptions.BypassThrottlingWithoutActivityUpdates);

            if (!result.IsSuccess || result.Response is null)
                throw new InvalidOperationException($"Receiver ID '{receiverId}' could not be verified at IP address '{receiverIpAddress}'. {result.ErrorMessage ?? "The receiver could not be discovered."}");

            if (!string.Equals(result.Response.StbChipID, receiverId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Receiver ID '{receiverId}' is not valid for IP address '{receiverIpAddress}'.");
        }

        public Task<CommandResult<HELLO_DISCOVERY_RESPONSE>> DiscoverReceiverAtIpAsync(string receiverIpAddress, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync<HELLO_DISCOVERY_RESPONSE>(receiverIpAddress, "0", HELLO_DISCOVERY.Default, timeout, cancellationToken, ReceiverCommandExecutionOptions.BypassThrottlingWithoutActivityUpdates);
        }

        public void SetReceiverActivityStatus(Guid receiverId, ReceiverActivityStatus activityStatus)
        {
            Receiver receiver = _receiverManager.GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);
            receiver.SetActivityStatus(activityStatus);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));
        }

        public async Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(string receiverIpAddress, string receiverId, IReceiverCommand command, TimeSpan? timeout = null, CancellationToken cancellationToken = default, ReceiverCommandExecutionOptions? executionOptions = null)
        {
            ArgumentNullException.ThrowIfNull(command);

            _logger.LogInformation("Begin Command Transaction for ID: {CommandId}", command.Id);
            _logger.LogInformation("[{CommandId}]: Command Type = '{Command}'", command.Id, command.Command);

            timeout ??= TimeSpan.FromSeconds(5);
            executionOptions ??= ReceiverCommandExecutionOptions.Default;

            if (!IPAddress.TryParse(receiverIpAddress, out IPAddress? ipAddress))
            {
                _logger.LogWarning("[{CommandId}]: '{ReceiverIPAddress}' is not a valid IP address.", command.Id, receiverIpAddress);
                return CommandResult<TResponse>.Failure($"'{receiverIpAddress}' is not a valid IP address.");
            }

            if (!uint.TryParse(receiverId, out uint parsedReceiverId))
            {
                _logger.LogWarning("[{CommandId}]: '{ReceiverId}' is not a valid receiver ID.", command.Id, receiverId);
                return CommandResult<TResponse>.Failure($"'{receiverId}' is not a valid receiver ID.");
            }

            _logger.LogInformation("[{CommandId}]: Receiver ID: {ReceiverId}", command.Id, receiverId);
            _logger.LogInformation("[{CommandId}]: Receiver Address: {IPAddress}:{Port}", command.Id, receiverIpAddress, ReceiverPort);

            if (!executionOptions.BypassThrottle)
            {
                try
                {
                    await WaitForCommandWindowAsync(receiverId, command, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("[{CommandId}]: The receiver command was cancelled while waiting for the command throttle.", command.Id);
                    return CommandResult<TResponse>.Failure("The receiver command was cancelled.");
                }
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeoutSource.CancelAfter(timeout.Value);

            try
            {
                string commandJson = JsonSerializer.Serialize(command, command.GetType(), SerializerOptions);

                string requestText = $"{parsedReceiverId}:msg: {commandJson}";

                _logger.LogDebug("[{CommandId}]: UDP Request created. Payload Below: \n{Request}", command.Id, requestText);
                _logger.LogDebug("[{CommandId}]: UDP Request Ended.", command.Id);
                byte[] requestBytes = Encoding.ASCII.GetBytes(requestText);

                using var udpClient = new UdpClient();

                udpClient.Connect(new IPEndPoint(ipAddress, ReceiverPort));
                _logger.LogInformation("[{CommandId}]: UDP client connected.", command.Id);

                await udpClient.SendAsync(requestBytes, timeoutSource.Token);
                _logger.LogInformation("[{CommandId}]: UDP bytes sent.", command.Id);

                while (true)
                {
                    UdpReceiveResult received = await udpClient.ReceiveAsync(timeoutSource.Token);

                    string rawResponse = Encoding.ASCII.GetString(received.Buffer);

                    _logger.LogDebug("[{CommandId}]: UDP Response received. Response Below: \n{Response}", command.Id, rawResponse);
                    _logger.LogDebug("[{CommandId}]: UDP ResponseEnded.", command.Id);

                    if (!TryGetResponseCommandId(rawResponse, out int responseCommandId))
                    {
                        // Hello Discovery may return only its details object.
                        // If so, attempt to parse it without details
                        if (TryDeserializeResponse(rawResponse, out TResponse? directResponse))
                        {
                            _logger.LogInformation("[{CommandId}]: UDP response successfully received.", command.Id);
                            return CommandResult<TResponse>.Success(directResponse);
                        }

                        continue;
                    }

                    if (responseCommandId != command.Id)
                        continue;

                    if (!TryDeserializeResponse(rawResponse, out TResponse? response))
                    {
                        _logger.LogWarning("[{CommandId}]: The receiver responded, but the response could not be parsed.", command.Id);
                        return CommandResult<TResponse>.Failure("The receiver responded, but the response could not be parsed.");
                    }

                    _logger.LogInformation("[{CommandId}]: UDP response successfully received.", command.Id);
                    return CommandResult<TResponse>.Success(response);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("[{CommandId}]: The receiver did not respond within {Timeout:0.#} seconds.", command.Id, timeout.Value.TotalSeconds);
                return CommandResult<TResponse>.Failure($"The receiver did not respond within {timeout.Value.TotalSeconds:0.#} seconds.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[{CommandId}]: The receiver command was cancelled.", command.Id);
                return CommandResult<TResponse>.Failure("The receiver command was cancelled.");
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "[{CommandId}]: A socket error has occurred.", command.Id);
                return CommandResult<TResponse>.Failure($"A socket error occurred: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[{CommandId}]: The receiver returned invalid JSON.", command.Id);
                return CommandResult<TResponse>.Failure($"The receiver returned invalid JSON: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CommandId}]: An unexpected exception has occurred.", command.Id);
                return CommandResult<TResponse>.Failure($"An unexpected command error occurred: {ex.Message}", ex);
            }
        }

        public async Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(Guid receiverId, IReceiverCommand command, TimeSpan? timeout = null, CancellationToken cancellationToken = default, ReceiverCommandExecutionOptions? executionOptions = null)
        {
            Receiver receiver = _receiverManager.GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);
            executionOptions ??= ReceiverCommandExecutionOptions.Default;

            if (executionOptions.UpdateActivityStatus)
                BeginReceiverCommand(receiver);

            try
            {
                return await SendCommandAsync<TResponse>(receiver.Configuration.IpAddress, receiver.Configuration.ReceiverId, command, timeout, cancellationToken, executionOptions);
            }
            finally
            {
                if (executionOptions.UpdateActivityStatus)
                    EndReceiverCommand(receiver);
            }
        }

        private void BeginReceiverCommand(Receiver receiver)
        {
            lock (_receiverActivityLock)
            {
                _activeReceiverCommandCounts.TryGetValue(receiver.Id, out int activeCommandCount);
                _activeReceiverCommandCounts[receiver.Id] = activeCommandCount + 1;

                if (activeCommandCount > 0)
                    return;

                receiver.SetActivityStatus(ReceiverActivityStatus.Transmitting);
                ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));
            }
        }

        private void EndReceiverCommand(Receiver receiver)
        {
            lock (_receiverActivityLock)
            {
                if (!_activeReceiverCommandCounts.TryGetValue(receiver.Id, out int activeCommandCount))
                    return;

                if (activeCommandCount > 1)
                {
                    _activeReceiverCommandCounts[receiver.Id] = activeCommandCount - 1;
                    return;
                }

                _activeReceiverCommandCounts.Remove(receiver.Id);
                receiver.SetActivityStatus(ReceiverActivityStatus.Idle);
                ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));
            }
        }

        private async Task<DiscoverReceiverResult> DiscoverReceiverAsync(Guid receiverId, CancellationToken cancellationToken = default)
        {
            Receiver receiver = _receiverManager.GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);

            receiver.SetConnectionStatus(ReceiverConnectionStatus.Reconnecting);
            ReceiverConnectionStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            receiver.SetActivityStatus(ReceiverActivityStatus.Transmitting);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            CommandResult<HELLO_DISCOVERY_RESPONSE> result = await SendCommandAsync<HELLO_DISCOVERY_RESPONSE>(receiverId, HELLO_DISCOVERY.Default, TimeSpan.FromSeconds(5), cancellationToken, ReceiverCommandExecutionOptions.BypassThrottlingWithoutActivityUpdates);

            receiver.SetActivityStatus(ReceiverActivityStatus.Loading);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            if (!result.IsSuccess || result.Response is null)
            {
                receiver.SetConnectionStatus(ReceiverConnectionStatus.Offline, new ReceiverError()
                {
                    ErrorCode = "DISCOVERY_FAILED",
                    Message = $"Failed to discover the receiver '{receiver.Configuration.Name}'.",
                    InnerMessage = result.ErrorMessage,
                    OccurredAtUtc = DateTime.UtcNow,
                    Operation = "DiscoverReceiverAsync"
                });
                ReceiverConnectionStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

                receiver.SetActivityStatus(ReceiverActivityStatus.Idle);
                ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

                return new DiscoverReceiverResult
                {
                    DiscoveryResult = result,
                    EpgLoadTask = Task.CompletedTask
                };
            }

            HELLO_DISCOVERY_RESPONSE response = result.Response;

            if (!string.Equals(response.StbChipID, receiver.Configuration.ReceiverId, StringComparison.Ordinal))
            {
                receiver.SetConnectionStatus(ReceiverConnectionStatus.Offline, new ReceiverError()
                {
                    ErrorCode = "DISCOVERY_FAILED_INVALID_ID",
                    Message = $"The receiver '{receiver.Configuration.Name}' reported an ID that is different from '{receiver.Configuration.ReceiverId}'. Please ensure the correct ID is used.",
                    OccurredAtUtc = DateTime.UtcNow,
                    Operation = "DiscoverReceiverAsync"
                });
                ReceiverConnectionStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

                receiver.SetActivityStatus(ReceiverActivityStatus.Idle);
                ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

                return new DiscoverReceiverResult
                {
                    DiscoveryResult = CommandResult<HELLO_DISCOVERY_RESPONSE>.Failure(receiver.LastError?.Message ?? $"The receiver '{receiver.Configuration.Name}' reported an ID that is different from '{receiver.Configuration.ReceiverId}'. Please ensure the correct ID is used."),
                    EpgLoadTask = Task.CompletedTask
                };
            }

            receiver.UpdateDeviceInformation(new()
            {
                ReceiverId = receiver.Configuration.ReceiverId,
                TunerSoftwareVersion = response.TunerSWInfo,
                TunerSoftwareBuildInformation = response.TunerSWBuildInfo,
                DeviceInformation = response.DeviceInfo,
                ApkVersion = response.ApkVersion,
                IpAssignment = response.IpAssignment,
                EthernetMacAddress = response.EthernetMac,
                TimestampUtc = response.Timestamp
            });

            receiver.SetConnectionStatus(ReceiverConnectionStatus.Online);
            ReceiverConnectionStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            return new DiscoverReceiverResult
            {
                DiscoveryResult = CommandResult<HELLO_DISCOVERY_RESPONSE>.Failure(receiver.LastError?.Message ?? $"The receiver '{receiver.Configuration.Name}' reported an ID that is different from '{receiver.Configuration.ReceiverId}'. Please ensure the correct ID is used."),
                EpgLoadTask = DiscoverReceiverEPG(receiverId, cancellationToken)
            };
        }

        private async Task InvokeReceiverEPG(Guid receiverId, Task EPGTask)
        {
            Receiver receiver = _receiverManager.GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);
            try
            {
                await EPGTask;
            }
            catch (Exception ex)
            {
                receiver.SetActivityStatus(ReceiverActivityStatus.Idle, new ReceiverError()
                {
                    ErrorCode = "EPG_DISCOVERY_FAILED",
                    Message = $"Unable to retrieve EPG information for '{receiver.Configuration.Name}'.",
                    InnerMessage = ex.Message,
                    OccurredAtUtc = DateTime.UtcNow,
                    Operation = "InvokeReceiverEPG"
                });
                ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));
            }
        }

        private async Task DiscoverReceiverEPG(Guid receiverId, CancellationToken cancellationToken = default)
        {
            Receiver receiver = _receiverManager.GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);

            IReceiverCommand command = CMD_GET_LIST_EPG.Default;
            command.AddPayload(new CMD_PAYLOAD(serviceId: 101));

            receiver.SetActivityStatus(ReceiverActivityStatus.Transmitting);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            CommandResult<CMD_GET_LIST_EPG_RESPONSE<List<CMD_GET_LIST_EPG_DETAILS>>> result1 = await SendCommandAsync<CMD_GET_LIST_EPG_RESPONSE<List<CMD_GET_LIST_EPG_DETAILS>>>(receiverId, command, TimeSpan.FromSeconds(5), cancellationToken, ReceiverCommandExecutionOptions.BypassThrottlingWithoutActivityUpdates);

            receiver.SetActivityStatus(ReceiverActivityStatus.Loading);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            if (!result1.IsSuccess ||
                result1.Response == null ||
                result1.Response.Details is null ||
                result1.Response.Details.Count == 0 ||
                receiver.DeviceInformation == null ||
                !ushort.TryParse(result1.Response.Details[0].ServiceId.ToString(), out ushort EpgServiceId))
            {
                receiver.UpdateChannelInformation(null);
                receiver.SetActivityStatus(ReceiverActivityStatus.Idle);
                ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));
                return;
            }

            command = new CMD_GET_CURRENT_EPG(receiver.DeviceInformation.IsNewVersion);
            command.AddPayload(new CMD_PAYLOAD(serviceId: EpgServiceId));

            receiver.SetActivityStatus(ReceiverActivityStatus.Transmitting);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            CommandResult<CMD_GET_LIST_EPG_RESPONSE<CMD_GET_CURRENT_EPG_DETAILS>> result2 = await SendCommandAsync<CMD_GET_LIST_EPG_RESPONSE<CMD_GET_CURRENT_EPG_DETAILS>>(receiverId, command, TimeSpan.FromSeconds(5), cancellationToken, ReceiverCommandExecutionOptions.BypassThrottlingWithoutActivityUpdates);

            receiver.SetActivityStatus(ReceiverActivityStatus.Loading);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            if (!result2.IsSuccess || result2.Response?.Details is null)
            {
                receiver.UpdateChannelInformation(null);
                receiver.SetActivityStatus(ReceiverActivityStatus.Idle);
                ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));
                return;
            }

            receiver.UpdateChannelInformation(new()
            {
                ServiceId = result2.Response.Details.ServiceId,
                ChannelName = result2.Response.Details.ChannelName,
                EventId = result2.Response.Details.EventId,
                EventName = result2.Response.Details.EventTitle,
                ShortDescription = result2.Response.Details.ShortDescription,
                LongDescription = result2.Response.Details.Description,
                StartTimeUtc = result2.Response.Details.StartTime,
                EndTimeUtc = result2.Response.Details.EndTime,
                Duration = result2.Response.Details.Duration,
                RemainingTime = result2.Response.Details.DurationLeft,
                IsRecording = result2.Response.Details.IsRecording,
                TimerStatusPassed = result2.Response.Details.TimerStatusPassed
            });

            receiver.SetActivityStatus(ReceiverActivityStatus.Idle);
            ReceiverActivityStatusChanged?.Invoke(this, new ReceiverChangedEventArgs(receiver));

            return;
        }

        private async Task WaitForCommandWindowAsync(string receiverId, IReceiverCommand command, CancellationToken cancellationToken)
        {
            ReceiverCommandThrottleState throttleState = _commandThrottleStates.GetOrAdd(receiverId, _ => new ReceiverCommandThrottleState());

            await throttleState.Gate.WaitAsync(cancellationToken);

            try
            {
                TimeSpan delay = throttleState.NextAllowedAtUtc - DateTimeOffset.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogDebug("[{CommandId}]: Waiting {ThrottleDelay:0.###} seconds for the receiver command throttle.", command.Id, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }

                throttleState.NextAllowedAtUtc = DateTimeOffset.UtcNow.Add(MinimumCommandInterval);
            }
            finally
            {
                throttleState.Gate.Release();
            }
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
            return responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(IReceiverResponse);
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

        private sealed class ReceiverCommandThrottleState
        {
            public SemaphoreSlim Gate { get; } = new(1, 1);

            public DateTimeOffset NextAllowedAtUtc { get; set; } = DateTimeOffset.MinValue;
        }
    }

    public sealed class DiscoverReceiverResult
    {
        public required CommandResult<HELLO_DISCOVERY_RESPONSE> DiscoveryResult { get; init; }

        public required Task EpgLoadTask { get; init; }
    }
}
