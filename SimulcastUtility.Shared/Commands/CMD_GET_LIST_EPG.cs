using SimulcastUtility.Shared.Json;
using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Shared.Commands
{
    public class CMD_GET_LIST_EPG : CMD_STB_MESSAGE
    {
        public CMD_GET_LIST_EPG()
        {
            Id = CommandIdGenerator.Next();
            ApiKey = "dca15ceb-39c9-49f8-a0a6-a85c7402af6e";
            Command = "CMD_GET_LIST_EPG";
            Description = "Get LIST EPG for given service ID";
            Payload = new CMD_STB_MESSAGE_PAYLOAD(serviceId: 101);
        }
    }

    public class CMD_GET_LIST_EPG_RESPONSE
    {

        [JsonPropertyName("startTimeUtcMillis")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(DateTimeUTCMillisecondJsonConverter))]
        public DateTime? StartTime { get; set; }

        [JsonPropertyName("durationTimeUtcMillis")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(TimeSpanUTCMillisecondsJsonConverter))]
        public TimeSpan? Duration { get; set; }

        [JsonPropertyName("remainingTimeUtcMillis")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(TimeSpanUTCMillisecondsJsonConverter))]
        public TimeSpan? DurationLeft { get; set; }

        [JsonPropertyName("endTimeUtcMillis")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(DateTimeUTCMillisecondJsonConverter))]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("s_id")]
        public int ServiceId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
    }
}
