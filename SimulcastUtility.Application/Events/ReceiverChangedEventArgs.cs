using SimulcastUtility.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application.Events
{
    public sealed class ReceiverChangedEventArgs : EventArgs
    {
        public Receiver Receiver { get; }

        public ReceiverChangedEventArgs(Receiver receiver)
        {
            Receiver = receiver;
        }
    }
}
