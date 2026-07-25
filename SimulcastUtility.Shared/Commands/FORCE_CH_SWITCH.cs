using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Shared.Commands
{
    public class FORCE_CH_SWITCH : CMD_STB_MESSAGE
    {
        public FORCE_CH_SWITCH(int ServiceId)
        {
            Id = CommandIdGenerator.Next();
            ApiKey = "dca15ceb-39c9-49f8-a0a6-a85c7402af6e";
            Command = "FORCE_CH_SWITCH";
            Description = "Forces a channel switch to a given service.";
            Payload = new CMD_STB_MESSAGE_PAYLOAD(serviceId: (ushort)ServiceId);
        }
    }
}
