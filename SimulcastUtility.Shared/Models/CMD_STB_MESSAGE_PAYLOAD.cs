using SimulcastUtility.Shared.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Shared.Models
{
    public class CMD_STB_MESSAGE_PAYLOAD
    {
        [JsonPropertyName("service_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ushort? ServiceID { get; set; }

        [JsonPropertyName("button_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ButtonKey { get; set; }

        [JsonPropertyName("record_start")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(DateTimeUnixTimestampJsonConverter))]
        public DateTime? RecordStart { get; set; }

        [JsonPropertyName("record_stop")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(DateTimeUnixTimestampJsonConverter))]
        public DateTime? RecordStop { get; set; }

        [JsonPropertyName("duration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? Duration { get; set; }

        [JsonPropertyName("recording_uuid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? RecordingUuid { get; set; }

        public CMD_STB_MESSAGE_PAYLOAD(ushort? serviceId = null, 
            string? buttonKey = null, 
            DateTime? recordStart = null, 
            DateTime? recordStop = null, 
            uint? duration = null, 
            Guid? recordingUuid = null)
        {
            ServiceID = serviceId;
            ButtonKey = buttonKey;
            RecordStart = recordStart;
            RecordStop = recordStop;
            Duration = duration;
            RecordingUuid = recordingUuid;
        }
    }
}
