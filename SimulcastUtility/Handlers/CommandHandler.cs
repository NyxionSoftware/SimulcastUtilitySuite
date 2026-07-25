using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SimulcastUtility.Handlers
{
    public static class CommandHandler
    {
        private const int ReceiverPort = 25671;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(Receiver receiver, CMD_STB_MESSAGE command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            return SendCommandAsync<TResponse>(receiver.IpAddress, receiver.ReceiverId, command, timeout, cancellationToken);
        }

        public static async Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(string receiverIpAddress, string receiverId, CMD_STB_MESSAGE command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

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
                        // Hello Discovery may return only its details object.
                        // If so, attempt to parse it without details
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
