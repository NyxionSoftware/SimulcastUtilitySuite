using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Plugin.Abstractions.Events
{
    public class ReceiverStatusChangedEventArgs : EventArgs
    {
        public ReceiverStatusChangedEventArgs(Receiver? receiver, ReceiverStatus status)
        {
            Receiver = receiver;
            Status = status;
        }

        public Receiver? Receiver { get; }
        public ReceiverStatus? Status { get; }
    }
}
