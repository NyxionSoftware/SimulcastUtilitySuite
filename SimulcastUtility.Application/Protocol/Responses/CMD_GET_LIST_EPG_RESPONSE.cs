using SimulcastUtility.Application.Converters;
using SimulcastUtility.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Protocol.Responses
{
    public class CMD_GET_LIST_EPG_RESPONSE<TDetails> : IReceiverResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("command")]
        public string Command { get; set; }

        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TDetails? Details { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("timestamp")]
        [JsonConverter(typeof(DateTimeUnixTimestampJsonConverter))]
        public DateTime Timestamp { get; set; }
    }
}
