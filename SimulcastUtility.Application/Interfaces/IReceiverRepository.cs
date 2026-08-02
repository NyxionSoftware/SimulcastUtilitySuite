using SimulcastUtility.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application.Interfaces
{
    public interface IReceiverRepository
    {
        Task<IReadOnlyList<Receiver>> LoadAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(IEnumerable<Receiver> receivers, CancellationToken cancellationToken = default);
    }
}
