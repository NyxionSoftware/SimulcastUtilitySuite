using SimulcastUtility.Shared.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Shared.Models
{
    public sealed class Receiver : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _receiverId = string.Empty;
        private string _ipAddress = string.Empty;

        private string? _apkVersion;
        private DateTime? _versionDate;

        private string _tunerSoftwareVersion = string.Empty;
        private string _tunerSoftwareBuildInfo = string.Empty;
        private string? _ethernetMac;
        private string _deviceInfo = string.Empty;

        private int _channel;
        private string _channelTitle;
        private string _channelName;
        private DateTime? _channelStartTime;
        private DateTime? _channelEndTime;
        private TimeSpan? _channelDuration;
        private TimeSpan? _channelRemainingTime;

        private ReceiverStatus _status = ReceiverStatus.Offline;
        private string? _lastError;
        private DateTime _lastRefreshUtc = DateTime.MinValue;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        [JsonIgnore]
        public ReceiverStatus Status
        {
            get => _status;
            set
            {
                if (!SetField(ref _status, value))
                    return;

                OnPropertyChanged(nameof(CanExecuteActions));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public DateTime LastRefreshUtc
        {
            get => _lastRefreshUtc;
            set
            {
                if (_lastRefreshUtc == value)
                    return;

                _lastRefreshUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRefresh));
            }
        }

        public bool CanRefresh => DateTime.UtcNow - LastRefreshUtc >= TimeSpan.FromSeconds(15);

        public string ReceiverId
        {
            get => _receiverId;
            set => SetField(ref _receiverId, value);
        }

        public string IpAddress
        {
            get => _ipAddress;
            set => SetField(ref _ipAddress, value);
        }
        [JsonIgnore]
        public string? ApkVersion
        {
            get => _apkVersion;
            set
            {
                if (!SetField(ref _apkVersion, value))
                    return;

                VersionDate = ReceiverVersionParser.ParseApkVersionDate(value);
            }
        }
        [JsonIgnore]
        public DateTime? VersionDate
        {
            get => _versionDate;
            private set
            {
                if (!SetField(ref _versionDate, value))
                    return;

                OnPropertyChanged(nameof(IsNewVersion));
            }
        }
        [JsonIgnore]
        public bool IsNewVersion
        {
            get
            {
                return VersionDate is DateTime versionDate && versionDate >= ReceiverVersionParser.NewVersionCutoff;
            }
        }
        [JsonIgnore]
        public string TunerSoftwareVersion
        {
            get => _tunerSoftwareVersion;
            set => SetField(ref _tunerSoftwareVersion, value);
        }
        [JsonIgnore]
        public string TunerSoftwareBuildInfo
        {
            get => _tunerSoftwareBuildInfo;
            set => SetField(ref _tunerSoftwareBuildInfo, value);
        }
        [JsonIgnore]
        public string? EthernetMac
        {
            get => _ethernetMac;
            set => SetField(ref _ethernetMac, value);
        }
        [JsonIgnore]
        public string DeviceInfo
        {
            get => _deviceInfo;
            set => SetField(ref _deviceInfo, value);
        }
        [JsonIgnore]
        public int Channel
        {
            get => _channel;
            set => SetField(ref _channel, value);
        }
        [JsonIgnore]
        public string ChannelName
        {
            get => _channelName;
            set => SetField(ref _channelName, value);
        }
        [JsonIgnore]
        public DateTime? ChannelStartTime
        {
            get => _channelStartTime;
            set => SetField(ref _channelStartTime, value);
        }
        [JsonIgnore]
        public DateTime? ChannelEndTime
        {
            get => _channelEndTime;
            set => SetField(ref _channelEndTime, value);
        }
        [JsonIgnore]
        public string ChannelTitle
        {
            get => _channelTitle;
            set => SetField(ref _channelTitle, value);
        }
        [JsonIgnore]
        public TimeSpan? ChannelDuration
        {
            get => _channelDuration;
            set => SetField(ref _channelDuration, value);
        }
        [JsonIgnore]
        public TimeSpan? ChannelRemainingTime
        {
            get => _channelRemainingTime;
            set => SetField(ref _channelRemainingTime, value);
        }

        [JsonIgnore]
        public string? LastError
        {
            get => _lastError;
            set => SetField(ref _lastError, value);
        }

        [JsonIgnore]
        public bool CanExecuteActions => Status == ReceiverStatus.Online;

        [JsonIgnore]
        public string StatusText
        {
            get
            {
                switch (_status)
                {
                    case ReceiverStatus.Offline:
                        return "Offline";
                    case ReceiverStatus.Online:
                        return "Online";
                    case ReceiverStatus.Loading:
                        return "Loading...";
                    case ReceiverStatus.Editing:
                        return "Editing...";
                    default:
                        return "Error";
                }
            }
        }

        [JsonIgnore]
        public SemaphoreSlim CommandLock { get; } = new(1, 1);

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;

            OnPropertyChanged(propertyName);

            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
