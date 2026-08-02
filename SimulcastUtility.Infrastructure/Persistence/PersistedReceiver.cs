using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Infrastructure.Persistence
{
    internal sealed class PersistedReceiver
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string ReceiverId { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;
    }
}
