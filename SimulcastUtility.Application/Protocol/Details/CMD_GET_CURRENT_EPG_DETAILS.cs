using SimulcastUtility.Application.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Protocol.Details
{
    public class CMD_GET_CURRENT_EPG_DETAILS
    {
        [JsonPropertyName("s_id")]
        public int ServiceId { get; set; }

        [JsonPropertyName("channelName")]
        public string ChannelName { get; set; } = string.Empty;

        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("title")]
        public string EventTitle { get; set; } = string.Empty;

        [JsonPropertyName("longDescription")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("shortDescription")]
        public string ShortDescription { get; set; } = string.Empty;

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

        [JsonPropertyName("recordTimer")]
        public bool IsRecording { get; set; }

        [JsonPropertyName("timerStatusPass")]
        public bool TimerStatusPassed { get; set; }

    }
}
