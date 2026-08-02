using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Exceptions
{
    public sealed class ReceiverNotFoundException : Exception
    {
        public Guid Identifier { get; }

        public ReceiverNotFoundException(Guid identifier) : base($"Receiver with the identifier '{identifier}' could not be found.")
        {
            Identifier = identifier;
        }
    }
}
