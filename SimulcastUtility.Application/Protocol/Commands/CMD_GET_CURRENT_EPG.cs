using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Protocol.Payloads;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Protocol.Commands
{
    public class CMD_GET_CURRENT_EPG : IReceiverCommand
    {
        [JsonIgnore]
        public static CMD_GET_CURRENT_EPG NewVersion => new(true);

        [JsonIgnore]
        public static CMD_GET_CURRENT_EPG OldVersion => new(false);

        [JsonPropertyName("id")]
        public int Id { get; set; } = CommandIdGenerator.Next();

        [JsonPropertyName("command")]
        public string Command { get; set; } = "CMD_GET_CURRENT_EPG";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "Get CURRENT EPG for current playback";

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = "dca15ceb-39c9-49f8-a0a6-a85c7402af6e";

        [JsonPropertyName("payload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CMD_PAYLOAD? Payload { get; set; }

        public CMD_GET_CURRENT_EPG(bool IsNewVersion)
        {
            Description = IsNewVersion ? "Get CURRENT EPG for current playback" : "Get EPG for the given service ID.";
        }

        public void AddPayload(CMD_PAYLOAD? payload)
        {
            Payload = payload;
        }
    }
}
