using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace SimulcastUtility.Plugin.Abstractions.Events
{
    public class ReceiverCommandInvokedEventArgs : EventArgs
    {
        public ReceiverCommandInvokedEventArgs(string receiverId, CMD_STB_MESSAGE command, bool successful, string? error)
        {
            ReceiverId = receiverId;
            Command = command;
            Successful = successful;
            Error = error;
        }

        public string ReceiverId { get; }
        public CMD_STB_MESSAGE Command { get; }
        public bool Successful { get; }
        public string? Error { get; }
    }
}
