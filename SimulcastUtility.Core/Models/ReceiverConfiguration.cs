using SimulcastUtility.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Models
{
    public sealed class ReceiverConfiguration
    {
        public string Name { get; private set; }

        public string ReceiverId { get; private set; }

        public string IpAddress { get; private set; }

        public static ReceiverConfiguration Create(string name, string receiverId, string ipAddress)
        {
            return new ReceiverConfiguration
            {
                Name = name,
                ReceiverId = receiverId,
                IpAddress = ipAddress
            };
        }
    }
}
