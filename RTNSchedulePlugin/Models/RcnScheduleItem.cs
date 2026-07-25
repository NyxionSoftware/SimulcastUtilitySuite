using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RTNSchedulePlugin.Models
{
    public sealed class RcnScheduleItem
    {
        public string ChannelNumber { get; init; } = string.Empty;

        public string EventName { get; init; } = string.Empty;

        public DateTime? StartTime { get; init; }

        public TimeSpan? Duration { get; init; }

        public string OriginalOnAirTime { get; init; } = string.Empty;

        public string OriginalDuration { get; init; } = string.Empty;

        public string FormattedStartTime => StartTime?.ToString("h:mm tt") ?? OriginalOnAirTime;

        public string FormattedEndTime
        {
            get
            {
                if (StartTime is DateTime startTime && Duration is TimeSpan duration)
                    return (startTime + duration).ToString("h:mm tt");

                return OriginalDuration;
            }
        }
    }

    public sealed record RcnScheduleResult(string Title, IReadOnlyList<RcnScheduleItem> Items);
}
