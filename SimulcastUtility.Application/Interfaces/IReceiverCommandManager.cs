using SimulcastUtility.Application.Events;
using SimulcastUtility.Application.Protocol;
using SimulcastUtility.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application.Interfaces
{
    public interface IReceiverCommandManager
    {

        event EventHandler<ReceiverChangedEventArgs>? ReceiverConnectionStatusChanged;
        event EventHandler<ReceiverChangedEventArgs>? ReceiverActivityStatusChanged;

        Task RefreshReceiverAsync(Guid receiverId, CancellationToken cancellationToken = default);

        Task RefreshReceiverEpgAsync(Guid receiverId, CancellationToken cancellationToken = default);

        Task VerifyReceiverAsync(string receiverIpAddress, string receiverId, CancellationToken cancellationToken = default);

        void SetReceiverActivityStatus(Guid receiverId, ReceiverActivityStatus activityStatus);

        Task RefreshAllReceiversAsync(CancellationToken cancellationToken = default);

        Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(Guid receiverId, IReceiverCommand command, TimeSpan? timeout = null, CancellationToken cancellationToken = default, ReceiverCommandExecutionOptions? executionOptions = null);

        Task<CommandResult<TResponse>> SendCommandAsync<TResponse>(string receiverIpAddress, string receiverId, IReceiverCommand command, TimeSpan? timeout = null, CancellationToken cancellationToken = default, ReceiverCommandExecutionOptions? executionOptions = null);
    }
}
