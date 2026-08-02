using SimulcastUtility.Application.Protocol.Payloads;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Interfaces
{
    public interface IReceiverCommand
    {
        public int Id { get; set; }

        public string Command { get; set; }

        public string Description { get; set; }

        public CMD_PAYLOAD? Payload { get; set; }

        void AddPayload(CMD_PAYLOAD? payload)
        {
            throw new NotSupportedException($"{GetType().Name} does not support adding of payloads.");
        }
    }
}
