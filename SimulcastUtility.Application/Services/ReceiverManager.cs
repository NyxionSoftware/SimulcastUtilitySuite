using Microsoft.Extensions.Logging;
using SimulcastUtility.Application.Events;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Requests;
using SimulcastUtility.Core.Enums;
using SimulcastUtility.Core.Exceptions;
using SimulcastUtility.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SimulcastUtility.Application.Services
{
    public class ReceiverManager : IReceiverManager, IReceiverManagerInitializer
    {
        private readonly IReceiverRepository _repository;
        private readonly ILogger<ReceiverManager> _logger;
        private readonly ObservableCollection<Receiver> _receivers = new();
        private readonly ReadOnlyObservableCollection<Receiver> _readOnlyReceivers;

        public IReadOnlyList<Receiver> Receivers => _readOnlyReceivers;

        public Receiver? SelectedReceiver { get; private set; }

        public event EventHandler<ReceiverSelectionChangedEventArgs>? SelectedReceiverChanged;

        public bool IsInitialized { get; private set; }

        public ReceiverManager(IReceiverRepository repository, ILogger<ReceiverManager> logger)
        {
            _repository = repository;
            _logger = logger;
            _readOnlyReceivers = new ReadOnlyObservableCollection<Receiver>(_receivers);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
                return;

            IReadOnlyList<Receiver> receivers = await _repository.LoadAsync(cancellationToken);

            _receivers.Clear();

            foreach (Receiver receiver in receivers)
                _receivers.Add(receiver);

            SetSelectedReceiver(_receivers.FirstOrDefault());
            IsInitialized = true;

            _logger.LogInformation("Receiver manager initialized with {ReceiverCount} receivers.", _receivers.Count);

            await Task.CompletedTask;
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            Guid? selectedReceiverId = SelectedReceiver?.Id;
            IReadOnlyList<Receiver> receivers = await _repository.LoadAsync(cancellationToken);
            _receivers.Clear();

            foreach (Receiver receiver in receivers)
                _receivers.Add(receiver);

            Receiver? selectedReceiver = selectedReceiverId is { } receiverId ? GetReceiver(receiverId) : null;
            SetSelectedReceiver(selectedReceiver ?? _receivers.FirstOrDefault());
            _logger.LogInformation("Receiver manager reloaded {ReceiverCount} receivers from storage.", _receivers.Count);
        }

        public Receiver? GetReceiver(Guid receiverId) => _receivers.FirstOrDefault(receiver => receiver.Id == receiverId);

        public void SelectReceiver(Guid? receiverId)
        {
            if (receiverId is null)
            {
                SetSelectedReceiver(null);
                return;
            }

            Receiver receiver = GetReceiver(receiverId.Value) ?? throw new InvalidOperationException($"Receiver '{receiverId}' could not be found.");

            SetSelectedReceiver(receiver);
        }

        public async Task<Receiver> AddReceiverAsync(ReceiverCreateRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            EnsureInitialized();
            EnsureReceiverIsUnique(request.Name, request.ReceiverId, request.IpAddress);

            ReceiverConfiguration configuration = ReceiverConfiguration.Create(request.Name, request.ReceiverId, request.IpAddress);

            Receiver receiver = Receiver.Create(configuration);

            _receivers.Add(receiver);

            try
            {
                await _repository.SaveAsync(_receivers, cancellationToken);

                _logger.LogInformation("Receiver {ReceiverName} ({ReceiverId}) was added.", receiver.Configuration.Name, receiver.Id);

                return receiver;
            }
            catch
            {
                _receivers.Remove(receiver);
                throw;
            }
        }

        public async Task<Receiver> UpdateReceiverAsync(Guid receiverId, ReceiverUpdateRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            EnsureInitialized();

            Receiver receiver = GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);

            EnsureReceiverIsUnique(request.Name, request.ReceiverId, request.IpAddress, receiverId);

            ReceiverConfiguration previousConfiguration = receiver.Configuration;

            ReceiverConfiguration updatedConfiguration = ReceiverConfiguration.Create(request.Name, request.ReceiverId, request.IpAddress);

            receiver.UpdateConfiguration(updatedConfiguration);

            try
            {
                await _repository.SaveAsync(_receivers, cancellationToken);

                _logger.LogInformation("Receiver {ReceiverName} ({ReceiverId}) was updated.", receiver.Configuration.Name, receiver.Id);

                return receiver;
            }
            catch
            {
                receiver.UpdateConfiguration(previousConfiguration);
                throw;
            }
        }

        public async Task RemoveReceiverAsync(Guid receiverId, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            Receiver receiver = GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);

            int originalIndex = _receivers.IndexOf(receiver);
            bool wasSelected = SelectedReceiver?.Id == receiver.Id;

            _receivers.RemoveAt(originalIndex);

            if (wasSelected)
                SetSelectedReceiver(GetReceiverAfterRemoval(originalIndex));

            try
            {
                await _repository.SaveAsync(_receivers, cancellationToken);

                _logger.LogInformation("Receiver {ReceiverName} ({ReceiverId}) was removed.", receiver.Configuration.Name, receiver.Id);
            }
            catch
            {
                _receivers.Insert(originalIndex, receiver);

                if (wasSelected)
                    SetSelectedReceiver(receiver);

                throw;
            }
        }

        public async Task MoveReceiverAsync(Guid receiverId, int newIndex, CancellationToken cancellationToken = default)
        {
            Receiver receiver = GetReceiver(receiverId) ?? throw new ReceiverNotFoundException(receiverId);

            newIndex = Math.Clamp(newIndex, 0, _receivers.Count - 1);

            int currentIndex = _receivers.IndexOf(receiver);

            if (currentIndex == newIndex)
                return;

            _receivers.Move(currentIndex, newIndex);

            await _repository.SaveAsync(_receivers, cancellationToken);
        }

        private void EnsureReceiverIsUnique(string name, string receiverId, string ipAddress, Guid? excludedReceiverId = null)
        {
            Receiver? duplicateReceiver = _receivers.FirstOrDefault(receiver =>
            receiver.Id != excludedReceiverId &&
            (
                string.Equals(receiver.Configuration.Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(receiver.Configuration.ReceiverId, receiverId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(receiver.Configuration.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase)
            ));

            if (duplicateReceiver is null)
                return;

            if (string.Equals(duplicateReceiver.Configuration.Name, name, StringComparison.OrdinalIgnoreCase))
                throw new DuplicateReceiverException(ReceiverDuplicateType.Name, name);

            if (string.Equals(duplicateReceiver.Configuration.ReceiverId, receiverId, StringComparison.OrdinalIgnoreCase))
                throw new DuplicateReceiverException(ReceiverDuplicateType.ReceiverId, receiverId);

            throw new DuplicateReceiverException(ReceiverDuplicateType.IPAddress, ipAddress);
        }

        private Receiver? GetReceiverAfterRemoval(int removedIndex)
        {
            if (_receivers.Count == 0)
                return null;

            int nextIndex = Math.Clamp(removedIndex, 0, _receivers.Count - 1);

            return _receivers[nextIndex];
        }

        private void SetSelectedReceiver(Receiver? receiver)
        {
            if (ReferenceEquals(SelectedReceiver, receiver))
                return;

            Receiver? previousReceiver = SelectedReceiver;
            SelectedReceiver = receiver;

            SelectedReceiverChanged?.Invoke(this, new ReceiverSelectionChangedEventArgs(previousReceiver, SelectedReceiver));
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("The receiver manager has not been initialized.");
        }
    }
}
