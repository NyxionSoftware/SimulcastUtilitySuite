using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Protocol.Payloads;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Protocol.Commands
{
    public class HELLO_DISCOVERY : IReceiverCommand
    {
        [JsonIgnore]
        public static HELLO_DISCOVERY Default => new();

        [JsonPropertyName("id")]
        public int Id { get; set; } = CommandIdGenerator.Next();

        [JsonPropertyName("command")]
        public string Command { get; set; } = "HELLO_DISCOVERY";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "Check if it is the STB";

        [JsonPropertyName("payload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CMD_PAYLOAD? Payload { get; set; } = null;

    }
}
