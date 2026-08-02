using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Models
{
    public sealed class ReceiverError
    {
        public string Message { get; init; } = string.Empty;
        public string? InnerMessage { get; init; }

        public string? ErrorCode { get; init; }

        public DateTimeOffset OccurredAtUtc { get; init; }

        public string? Operation { get; init; }
    }
}
