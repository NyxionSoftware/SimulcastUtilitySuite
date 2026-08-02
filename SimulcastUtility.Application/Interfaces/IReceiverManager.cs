using SimulcastUtility.Application.Events;
using SimulcastUtility.Application.Requests;
using SimulcastUtility.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application.Interfaces
{
    public interface IReceiverManager
    {
        IReadOnlyList<Receiver> Receivers { get; }

        Receiver? SelectedReceiver { get; }

        event EventHandler<ReceiverSelectionChangedEventArgs>? SelectedReceiverChanged;

        bool IsInitialized { get; }

        Receiver? GetReceiver(Guid receiverId);

        Task<Receiver> AddReceiverAsync(ReceiverCreateRequest request, CancellationToken cancellationToken = default);

        Task<Receiver> UpdateReceiverAsync(Guid receiverId, ReceiverUpdateRequest request, CancellationToken cancellationToken = default);

        Task RemoveReceiverAsync(Guid receiverId, CancellationToken cancellationToken = default);

        Task MoveReceiverAsync(Guid receiverId, int newIndex, CancellationToken cancellationToken = default);

        void SelectReceiver(Guid? receiverId);

    }
}
