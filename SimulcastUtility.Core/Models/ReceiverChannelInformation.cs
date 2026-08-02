using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Models
{
    public sealed class ReceiverChannelInformation
    {
        public int ServiceId { get; init; }

        public string ChannelName { get; init; } = string.Empty;

        public int EventId { get; init; }

        public string EventName { get; init; } = string.Empty;

        public string ShortDescription { get; init; } = string.Empty;

        public string LongDescription { get; init; } = string.Empty;

        public DateTime StartTimeUtc { get; init; }

        public DateTime EndTimeUtc { get; init; }

        public TimeSpan Duration { get; init; }

        public TimeSpan RemainingTime { get; init; }

        public bool IsRecording { get; init; }

        public bool TimerStatusPassed { get; init; }
    }
}
