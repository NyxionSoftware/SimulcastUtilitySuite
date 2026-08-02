using CommunityToolkit.Mvvm.ComponentModel;
using SimulcastUtility.Core.Enums;
using SimulcastUtility.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Wpf.ViewModels.Models
{
    public sealed class ReceiverViewModel : ObservableObject
    {
        public Receiver Model { get; }

        public Guid Id => Model.Id;

        public string Name => Model.Configuration.Name;

        public string ReceiverId => Model.Configuration.ReceiverId;

        public string IpAddress => Model.Configuration.IpAddress;

        public string? ApkVersion => Model.DeviceInformation?.ApkVersion;

        public DateTime? VersionDate => Model.DeviceInformation?.VersionDate;

        public string? EthernetMac => FormatMacAddress(Model.DeviceInformation?.EthernetMacAddress);

        public int? Channel => Model.ChannelInformation?.ServiceId;

        public string? ChannelName => Model.ChannelInformation?.ChannelName;

        public DateTime? ChannelStartTime => Model.ChannelInformation?.StartTimeUtc;

        public DateTime? ChannelEndTime => Model.ChannelInformation?.EndTimeUtc;

        public double ChannelProgress
        {
            get
            {
                if (ChannelStartTime is not { } startTime || ChannelEndTime is not { } endTime)
                    return 0;

                DateTime startUtc = startTime.ToUniversalTime();
                DateTime endUtc = endTime.ToUniversalTime();
                TimeSpan duration = endUtc - startUtc;

                if (duration <= TimeSpan.Zero)
                    return 0;

                double progress = (DateTime.UtcNow - startUtc).TotalMilliseconds / duration.TotalMilliseconds * 100;
                return Math.Clamp(progress, 0, 100);
            }
        }

        public ReceiverConnectionStatus ConnectionStatus => Model.ConnectionStatus;

        public ReceiverActivityStatus ActivityStatus => Model.ActivityStatus;

        public DateTimeOffset? LastSeenUtc => Model.LastSeenUtc;

        public DateTimeOffset? LastRefreshRequestedUtc => Model.LastRefreshRequestedUtc;

        public ReceiverError? LastError => Model.LastError;

        public bool IsOnline => ConnectionStatus == ReceiverConnectionStatus.Online;

        public bool IsOffline => ConnectionStatus == ReceiverConnectionStatus.Offline;

        public bool IsReconnecting => ConnectionStatus == ReceiverConnectionStatus.Reconnecting;

        public bool HasConnectionError => ConnectionStatus == ReceiverConnectionStatus.Error;

        public bool IsLoading => ActivityStatus == ReceiverActivityStatus.Loading;

        public bool IsIdle => ActivityStatus == ReceiverActivityStatus.Idle;

        public bool IsEditing => ActivityStatus == ReceiverActivityStatus.Editing;

        public bool IsTransmitting => ActivityStatus == ReceiverActivityStatus.Transmitting;

        public bool CanExecuteActions => ConnectionStatus == ReceiverConnectionStatus.Online && ActivityStatus is not ReceiverActivityStatus.Loading and not ReceiverActivityStatus.Editing;

        public bool CanSendChannelChange => IsOnline && IsIdle;

        public string ActivityText => ActivityStatus switch
        {
            ReceiverActivityStatus.Idle => "Idle",
            ReceiverActivityStatus.Loading => "Loading",
            ReceiverActivityStatus.Editing => "Editing",
            ReceiverActivityStatus.Transmitting => "Transmitting",
            _ => "Activity unknown"
        };

        public string StatusText
        {
            get
            {
                var name = Enum.GetName(ConnectionStatus);
                if (name != null)
                    return name;

                if (HasConnectionError)
                    return "Error";

                if (IsReconnecting)
                    return "Reconnecting";

                if (IsLoading)
                    return "Loading";

                if (IsEditing)
                    return "Editing";

                if (IsTransmitting)
                    return "Transmitting";

                return ConnectionStatus switch
                {
                    ReceiverConnectionStatus.Online => "Online",
                    ReceiverConnectionStatus.Offline => "Offline",
                    _ => "Unknown"
                };
            }
        }

        public ReceiverViewModel(Receiver model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        private static string? FormatMacAddress(string? macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
                return macAddress;

            string normalizedMacAddress = new(macAddress.Where(Uri.IsHexDigit).ToArray());

            if (normalizedMacAddress.Length != 12)
                return macAddress;

            return string.Join(":", Enumerable.Range(0, 6).Select(index => normalizedMacAddress.Substring(index * 2, 2))).ToUpperInvariant();
        }

        public void RefreshFromModel()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ReceiverId));
            OnPropertyChanged(nameof(IpAddress));
            OnPropertyChanged(nameof(ApkVersion));
            OnPropertyChanged(nameof(VersionDate));
            OnPropertyChanged(nameof(EthernetMac));
            OnPropertyChanged(nameof(Channel));
            OnPropertyChanged(nameof(ChannelName));
            OnPropertyChanged(nameof(ChannelStartTime));
            OnPropertyChanged(nameof(ChannelEndTime));
            OnPropertyChanged(nameof(ChannelProgress));
            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(ActivityStatus));
            OnPropertyChanged(nameof(LastSeenUtc));
            OnPropertyChanged(nameof(LastRefreshRequestedUtc));
            OnPropertyChanged(nameof(LastError));
            OnPropertyChanged(nameof(IsOnline));
            OnPropertyChanged(nameof(IsOffline));
            OnPropertyChanged(nameof(IsReconnecting));
            OnPropertyChanged(nameof(HasConnectionError));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsTransmitting));
            OnPropertyChanged(nameof(CanExecuteActions));
            OnPropertyChanged(nameof(CanSendChannelChange));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ActivityText));
        }

        public void RefreshChannelProgress()
        {
            OnPropertyChanged(nameof(ChannelProgress));
        }
    }
}
