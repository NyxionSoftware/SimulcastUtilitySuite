using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SimulcastUtility.Plugin.Abstractions.Interfaces
{
    public interface IReceiverConfigurationService
    {
        string ConfigurationDirectory { get; }
        string ConfigurationFile { get; }

        Task<ObservableCollection<Receiver>> LoadReceiversAsync(CancellationToken cancellationToken);

        Task SaveReceiversAsync(IEnumerable<Receiver> receivers, CancellationToken cancellationToken);
        Task SaveReceiverAsync(Receiver receiver, CancellationToken cancellationToken);

        Task<Dictionary<Receiver, bool>> DeleteReceiversAsync(IEnumerable<Receiver> receivers, CancellationToken cancellationToken);
        Task<bool> DeleteReceiverAsync(Receiver receiver, CancellationToken cancellationToken);
        Task<bool> DeleteReceiverAsync(string receiverId, CancellationToken cancellationToken);

        Task<bool> ReceiverExistsAsync(string receiverId, string ipAddress, CancellationToken cancellationToken);
    }
}
