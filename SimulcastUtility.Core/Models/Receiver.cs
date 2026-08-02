using SimulcastUtility.Core.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace SimulcastUtility.Core.Models
{
    public sealed class Receiver
    {
        public Guid Id { get; private set; }

        public ReceiverConfiguration Configuration { get; private set; }

        public ReceiverDeviceInformation? DeviceInformation { get; private set; }

        public ReceiverChannelInformation? ChannelInformation { get; private set; }

        public ReceiverConnectionStatus ConnectionStatus { get; private set; }

        public ReceiverActivityStatus ActivityStatus { get; private set; }

        public DateTimeOffset? LastSeenUtc { get; private set; }

        public DateTimeOffset? LastRefreshRequestedUtc { get; private set; }

        public ReceiverError? LastError { get; private set; }

        private Receiver()
        {
            Configuration = null!;
        }

        public static Receiver Create(ReceiverConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            return new Receiver
            {
                Id = Guid.NewGuid(),
                Configuration = configuration,
                ConnectionStatus = ReceiverConnectionStatus.Offline,
                ActivityStatus = ReceiverActivityStatus.Idle
            };
        }

        public static Receiver Restore(Guid id, ReceiverConfiguration configuration)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Receiver ID cannot be empty.", nameof(id));

            ArgumentNullException.ThrowIfNull(configuration);

            return new Receiver
            {
                Id = id,
                Configuration = configuration,
                ConnectionStatus = ReceiverConnectionStatus.Offline,
                ActivityStatus = ReceiverActivityStatus.Idle
            };
        }

        public void UpdateConfiguration(ReceiverConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            Configuration = configuration;
        }

        public void UpdateDeviceInformation(ReceiverDeviceInformation? deviceInformation = null)
        {
            DeviceInformation = deviceInformation;
        }

        public void UpdateChannelInformation(ReceiverChannelInformation? channelInformation = null)
        {
            ChannelInformation = channelInformation;
        }

        public void SetConnectionStatus(ReceiverConnectionStatus connectionStatus, ReceiverError? lastError = null)
        {
            if(connectionStatus == ReceiverConnectionStatus.Online)
            {
                LastSeenUtc = DateTime.UtcNow;
            }
            else
            {
                LastSeenUtc = null;
            }
            ConnectionStatus = connectionStatus;
            LastError = lastError;
        }

        public void SetActivityStatus(ReceiverActivityStatus activityStatus, ReceiverError? lastError = null)
        {
            ActivityStatus = activityStatus;
            LastError = lastError;
        }

        public void MarkRefreshRequested(DateTimeOffset requestedAtUtc)
        {
            LastRefreshRequestedUtc = requestedAtUtc;
        }
    }
}
