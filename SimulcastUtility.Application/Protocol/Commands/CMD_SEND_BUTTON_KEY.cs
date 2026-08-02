using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Protocol.Payloads;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Protocol.Commands
{
    public class CMD_SEND_BUTTON_KEY : IReceiverCommand
    {
        [JsonIgnore]
        public static CMD_SEND_BUTTON_KEY Default => new();

        [JsonPropertyName("id")]
        public int Id { get; set; } = CommandIdGenerator.Next();

        [JsonPropertyName("command")]
        public string Command { get; set; } = "CMD_SEND_BUTTON_KEY";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "Send KEY button press to STB.";

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = "dca15ceb-39c9-49f8-a0a6-a85c7402af6e";

        [JsonPropertyName("payload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CMD_PAYLOAD? Payload { get; set; }

        public void AddPayload(CMD_PAYLOAD? payload)
        {
            Payload = payload;
        }
    }
}
