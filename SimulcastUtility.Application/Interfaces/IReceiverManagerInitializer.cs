using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application.Interfaces
{
    public interface IReceiverManagerInitializer
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
