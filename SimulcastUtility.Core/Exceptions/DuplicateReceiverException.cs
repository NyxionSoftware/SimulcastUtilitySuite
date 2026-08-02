using SimulcastUtility.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Exceptions
{
    public sealed class DuplicateReceiverException : Exception
    {
        public ReceiverDuplicateType DuplicateType { get; }
        public string DuplicateValue { get; }
        public DuplicateReceiverException(ReceiverDuplicateType type, string value) : base($"A receiver already exist with the {Enum.GetName(type)} '{value}'.")
        {
            DuplicateType = type;
            DuplicateValue = value;
        }
    }
}
