using SimulcastUtility.Application.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Protocol.Responses
{
    public class HELLO_DISCOVERY_RESPONSE
    {
        [JsonPropertyName("stb_chip_id")]
        public string StbChipID { get; set; } = string.Empty;

        [JsonPropertyName("tuner_sw_info")]
        public string TunerSWInfo { get; set; } = string.Empty;

        [JsonPropertyName("tuner_sw_build_info")]
        public string TunerSWBuildInfo { get; set; } = string.Empty;

        [JsonPropertyName("device_info")]
        public string DeviceInfo { get; set; } = string.Empty;

        [JsonPropertyName("apk_version")]
        public string ApkVersion { get; set; } = string.Empty;

        [JsonPropertyName("ipAssignment")]
        public string IpAssignment { get; set; } = string.Empty;

        [JsonPropertyName("ethernet_mac")]
        public string EthernetMac { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        [JsonConverter(typeof(DateTimeUnixTimestampJsonConverter))]
        public DateTime Timestamp { get; set; }
    }
}
