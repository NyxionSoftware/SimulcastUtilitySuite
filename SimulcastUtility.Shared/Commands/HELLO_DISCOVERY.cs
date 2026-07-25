using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Shared.Commands
{
    public class HELLO_DISCOVERY : CMD_STB_MESSAGE
    {
        public HELLO_DISCOVERY()
        {
            Id = CommandIdGenerator.Next();
            Command = "HELLO_DISCOVERY";
            Description = "Check if it is the STB";
        }
    }

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

        [JsonPropertyName("date")]
        public DateTime CurrentDate { get; set; }
    }
}
