using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Plugin.Abstractions.Events
{
    public class ReceiverUpdatedEventArgs : EventArgs
    {
        public ReceiverUpdatedEventArgs(Receiver? receiver)
        {
            Receiver = receiver;
        }

        public Receiver? Receiver { get; }
    }
}
