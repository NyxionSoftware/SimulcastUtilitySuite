using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SimulcastUtility.Services
{
    public sealed class ReceiverConfigurationService : IReceiverConfigurationService
    {
        private string ConfigurationDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimulcastUtility");
        private string ConfigurationFile => Path.Combine(ConfigurationDirectory, "receivers.json");
        string IReceiverConfigurationService.ConfigurationDirectory => ConfigurationDirectory;
        string IReceiverConfigurationService.ConfigurationFile => ConfigurationFile;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public async Task<ObservableCollection<Receiver>> LoadReceiversAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(ConfigurationFile))
                return new ObservableCollection<Receiver>();

            try
            {
                await using FileStream stream = new(
                    ConfigurationFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                List<Receiver>? receivers = await JsonSerializer.DeserializeAsync<List<Receiver>>(stream, SerializerOptions, cancellationToken);

                return new ObservableCollection<Receiver>(receivers ?? new List<Receiver>());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (JsonException)
            {
                return new ObservableCollection<Receiver>();
            }
            catch (IOException)
            {
                return new ObservableCollection<Receiver>();
            }
            catch (UnauthorizedAccessException)
            {
                return new ObservableCollection<Receiver>();
            }
        }

        public async Task SaveReceiversAsync(IEnumerable<Receiver> receivers, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receivers);

            Directory.CreateDirectory(ConfigurationDirectory);

            string temporaryFile = ConfigurationFile + ".tmp";

            try
            {
                await using (FileStream stream = new(
                    temporaryFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(stream, receivers, SerializerOptions, cancellationToken);

                    await stream.FlushAsync(cancellationToken);
                }

                File.Move(temporaryFile, ConfigurationFile, overwrite: true);
            }
            catch
            {
                if (File.Exists(temporaryFile))
                {
                    try
                    {
                        File.Delete(temporaryFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors.
                    }
                }

                throw;
            }
        }
        public async Task SaveReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            ObservableCollection<Receiver> receivers = await LoadReceiversAsync(cancellationToken);

            Receiver? existingReceiver = receivers.FirstOrDefault(existing => string.Equals(existing.ReceiverId, receiver.ReceiverId, StringComparison.Ordinal));

            if (existingReceiver is null)
            {
                receivers.Add(receiver);
            }
            else
            {
                int existingIndex = receivers.IndexOf(existingReceiver);

                receivers[existingIndex] = receiver;
            }

            await SaveReceiversAsync(receivers, cancellationToken);
        }


        public async Task<Dictionary<Receiver, bool>> DeleteReceiversAsync(IEnumerable<Receiver> receivers, CancellationToken cancellationToken = default)
        {
            var Transactions = new Dictionary<Receiver, bool>();
            foreach (Receiver receiver in receivers)
            {
                bool Successful = await DeleteReceiverAsync(receiver, cancellationToken);
                Transactions.Add(receiver, Successful);
            }
            return Transactions;
        }
        public async Task<bool> DeleteReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default)
        {
            if(receiver == null)
                return false;

            if (string.IsNullOrWhiteSpace(receiver.ReceiverId))
                return false;

            return await DeleteReceiverAsync(receiver.ReceiverId, cancellationToken);
        }
        public async Task<bool> DeleteReceiverAsync(string receiverId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(receiverId))
                return false;

            ObservableCollection<Receiver> receivers = await LoadReceiversAsync(cancellationToken);

            Receiver? receiver = receivers.FirstOrDefault(existing => string.Equals(existing.ReceiverId, receiverId, StringComparison.Ordinal));

            if (receiver is null)
                return false;

            receivers.Remove(receiver);

            await SaveReceiversAsync(receivers, cancellationToken);

            return true;
        }

        public async Task<bool> ReceiverExistsAsync(string receiverId, string ipAddress, CancellationToken cancellationToken = default)
        {
            ObservableCollection<Receiver> receivers = await LoadReceiversAsync(cancellationToken);

            return receivers.Any(receiver => string.Equals(receiver.ReceiverId, receiverId, StringComparison.Ordinal) || string.Equals(receiver.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase));
        }

    }
}
