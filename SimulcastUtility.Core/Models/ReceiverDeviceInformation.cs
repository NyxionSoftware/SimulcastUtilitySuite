using SimulcastUtility.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Models
{
    public sealed class ReceiverDeviceInformation
    {
        private string _apkVersion = string.Empty;

        public string ReceiverId { get; init; } = string.Empty;

        public string TunerSoftwareVersion { get; init; } = string.Empty;

        public string TunerSoftwareBuildInformation { get; init; } = string.Empty;

        public string DeviceInformation { get; init; } = string.Empty;

        public string ApkVersion
        {
            get => _apkVersion;
            set
            {
                _apkVersion = value ?? string.Empty;
                VersionDate = ReceiverVersionParser.ParseApkVersionDate(_apkVersion);
            }
        }

        public DateTime? VersionDate { get; set; }

        public bool IsNewVersion
        {
            get
            {
                return VersionDate is DateTime versionDate && versionDate >= ReceiverVersionParser.NewVersionCutoff;
            }
        }

        public string IpAssignment { get; init; } = string.Empty;

        public string EthernetMacAddress { get; init; } = string.Empty;

        public DateTimeOffset TimestampUtc { get; init; }
    }
}
