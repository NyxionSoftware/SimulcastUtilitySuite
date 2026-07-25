using SimulcastUtility.Shared.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Shared.Models
{
    public class CMD_STB_MESSAGE
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("api_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApiKey { get; set; }

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("trigger_time")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(DateTimeUnixTimestampJsonConverter))]
        public DateTime? TriggerTime { get; set; }

        [JsonPropertyName("payload")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CMD_STB_MESSAGE_PAYLOAD? Payload { get; set; }

        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? status { get; set; }
    }
}
