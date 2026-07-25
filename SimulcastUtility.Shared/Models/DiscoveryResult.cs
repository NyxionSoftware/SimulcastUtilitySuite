using SimulcastUtility.Shared.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Shared.Models
{
    public sealed class ReceiverDiscoveryResult
    {
        public required CommandResult<HELLO_DISCOVERY_RESPONSE> DiscoveryResult { get; init; }

        public required Task EpgLoadTask { get; init; }
    }
}
