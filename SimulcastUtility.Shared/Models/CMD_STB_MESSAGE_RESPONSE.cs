using SimulcastUtility.Shared.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Shared.Models
{
    public class CMD_STB_MESSAGE_RESPONSE<TDetails>
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TDetails? Details { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        [JsonConverter(typeof(DateTimeUnixTimestampJsonConverter))]
        public DateTime? Timestamp { get; set; }
    }
}
