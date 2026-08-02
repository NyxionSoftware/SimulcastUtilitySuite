using SimulcastUtility.Application.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Protocol.Details
{
    public class CMD_GET_LIST_EPG_DETAILS
    {
        [JsonPropertyName("s_id")]
        public int ServiceId { get; set; }

        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("title")]
        public string EventTitle { get; set; } = string.Empty;

        [JsonPropertyName("startTimeUtcMillis")]
        [JsonConverter(typeof(DateTimeUTCMillisecondJsonConverter))]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("durationTimeUtcMillis")]
        [JsonConverter(typeof(TimeSpanUTCMillisecondsJsonConverter))]
        public TimeSpan Duration { get; set; }

        [JsonPropertyName("remainingTimeUtcMillis")]
        [JsonConverter(typeof(TimeSpanUTCMillisecondsJsonConverter))]
        public TimeSpan DurationLeft { get; set; }

        [JsonPropertyName("endTimeUtcMillis")]
        [JsonConverter(typeof(DateTimeUTCMillisecondJsonConverter))]
        public DateTime EndTime { get; set; }

    }
}
