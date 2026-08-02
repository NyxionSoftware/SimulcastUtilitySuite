using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Core.Models;
using SimulcastUtility.Infrastructure.Options;
using SimulcastUtility.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SimulcastUtility.Infrastructure.Repository
{
    public sealed class JsonReceiverRepository : IReceiverRepository
    {
        private readonly JsonReceiverRepositoryOptions _options;
        private readonly ILogger<JsonReceiverRepository> _logger;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        public JsonReceiverRepository(IOptions<JsonReceiverRepositoryOptions> options, ILogger<JsonReceiverRepository> logger)
        {
            _options = options.Value;
            _logger = logger;

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        public async Task<IReadOnlyList<Receiver>> LoadAsync(CancellationToken cancellationToken = default)
        {
            await _fileLock.WaitAsync(cancellationToken);

            try
            {
                string filePath = _options.GetFullPath();

                if (!File.Exists(filePath))
                {
                    _logger.LogInformation("Receiver configuration file does not exist at {FilePath}. An empty receiver list will be used.", filePath);

                    return Array.Empty<Receiver>();
                }

                await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

                List<PersistedReceiver>? persistedReceivers = await JsonSerializer.DeserializeAsync<List<PersistedReceiver>>(stream, _serializerOptions, cancellationToken);

                if (persistedReceivers is null || persistedReceivers.Count == 0)
                    return Array.Empty<Receiver>();

                List<Receiver> receivers = new(persistedReceivers.Count);
                HashSet<Guid> loadedReceiverIds = new();

                foreach (PersistedReceiver persistedReceiver in persistedReceivers)
                {
                    try
                    {
                        Guid receiverId = persistedReceiver.Id;

                        if (receiverId == Guid.Empty || !loadedReceiverIds.Add(receiverId))
                        {
                            Guid duplicateReceiverId = receiverId;
                            receiverId = Guid.NewGuid();
                            loadedReceiverIds.Add(receiverId);
                            _logger.LogWarning("Receiver {ReceiverName} had an invalid or duplicate ID {DuplicateReceiverId}. It was assigned the new ID {ReceiverId}.", persistedReceiver.Name, duplicateReceiverId, receiverId);
                        }

                        ReceiverConfiguration configuration = ReceiverConfiguration.Create(persistedReceiver.Name, persistedReceiver.ReceiverId, persistedReceiver.IpAddress);

                        Receiver receiver = Receiver.Restore(receiverId, configuration);

                        receivers.Add(receiver);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Receiver {ReceiverId} could not be loaded from {FilePath}.", persistedReceiver.Id, filePath);
                    }
                }

                _logger.LogInformation("Loaded {ReceiverCount} receivers from {FilePath}.", receivers.Count, filePath);

                return receivers;
            }
            catch (JsonException ex)
            {
                string filePath = _options.GetFullPath();

                _logger.LogError(ex, "Receiver configuration file {FilePath} contains invalid JSON.", filePath);

                throw new InvalidOperationException($"The receiver configuration file '{filePath}' contains invalid JSON.", ex);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveAsync(IEnumerable<Receiver> receivers, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receivers);

            await _fileLock.WaitAsync(cancellationToken);

            string? temporaryFilePath = null;

            try
            {
                string filePath = _options.GetFullPath();
                string? directoryPath = Path.GetDirectoryName(filePath);

                if (string.IsNullOrWhiteSpace(directoryPath))
                    throw new InvalidOperationException("The receiver configuration directory is invalid.");

                Directory.CreateDirectory(directoryPath);

                List<PersistedReceiver> persistedReceivers = receivers.Select(receiver => new PersistedReceiver
                {
                    Id = receiver.Id,
                    Name = receiver.Configuration.Name,
                    ReceiverId = receiver.Configuration.ReceiverId,
                    IpAddress = receiver.Configuration.IpAddress
                }).ToList();

                temporaryFilePath = filePath + ".tmp";

                await using (FileStream stream = new(temporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(stream, persistedReceivers, _serializerOptions, cancellationToken);

                    await stream.FlushAsync(cancellationToken);
                }

                File.Move(temporaryFilePath, filePath, overwrite: true);

                temporaryFilePath = null;

                _logger.LogInformation("Saved {ReceiverCount} receivers to {FilePath}.", persistedReceivers.Count, filePath);
            }
            finally
            {
                if (temporaryFilePath is not null && File.Exists(temporaryFilePath))
                {
                    try
                    {
                        File.Delete(temporaryFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Temporary receiver configuration file {FilePath} could not be deleted.", temporaryFilePath);
                    }
                }

                _fileLock.Release();
            }
        }
    }
}
