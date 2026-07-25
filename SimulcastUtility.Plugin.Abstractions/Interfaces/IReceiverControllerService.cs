using SimulcastUtility.Plugin.Abstractions.Events;
using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SimulcastUtility.Plugin.Abstractions.Interfaces
{
    public interface IReceiverControllerService
    {
        ReadOnlyObservableCollection<Receiver> Receivers { get; }
        Receiver? SelectedReceiver { get; set; }

        event EventHandler? ReceiversInitialized;
        event EventHandler? ReceiversReordered;

        event EventHandler<ReceiverEventArgs>? ReceiverAdded;
        event EventHandler<ReceiverEventArgs>? ReceiverRemoved;
        event EventHandler<ReceiverUpdatedEventArgs>? ReceiverUpdated;

        event EventHandler<ReceiverEventArgs>? ReceiverRefreshed;
        event EventHandler<ReceiverEventArgs>? ReceiverEPGRefreshed;

        event EventHandler<ReceiverEventArgs>? SelectedReceiverChanged;

        event EventHandler<ReceiverStatusChangedEventArgs>? ReceiverStatusChanged;

        event EventHandler<ReceiverCommandInvokedEventArgs>? ReceiverCommandInvoked;

        bool IsInitialized { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);

        Task<ReceiverOperationResult> AddReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default);

        Task<ReceiverOperationResult> UpdateReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default);

        Task<ReceiverOperationResult> RemoveReceiverAsync(Receiver receiver, CancellationToken cancellationToken = default);

        Task<ReceiverOperationResult> MoveReceiverAsync(Receiver draggedReceiver, Receiver targetReceiver, bool insertAfter = true, CancellationToken cancellationToken = default);
        Task<ReceiverOperationResult> MoveReceiverToIndexAsync(Receiver receiver, int targetIndex, bool saveChanges = true, CancellationToken cancellationToken = default);

        Task<ReceiverOperationResult> SaveReceiverOrderAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(CancellationToken cancellationToken = default);

        Task RefreshReceiverAsync(Receiver receiver, RefreshBehavior refreshBehavior = RefreshBehavior.Immediate, CancellationToken cancellationToken = default);

        Task RefreshAllReceiversAsync(CancellationToken cancellationToken = default);

        Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(Receiver receiver, CMD_STB_MESSAGE command, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(string receiverIpAddress, string receiverId, CMD_STB_MESSAGE command, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    }
}
