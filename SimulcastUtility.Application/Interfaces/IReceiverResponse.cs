using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application.Interfaces
{
    public interface IReceiverResponse
    {
        public int Id { get; set; }

        public string Command { get; set; }

        public string Message { get; set; }

        public string Status { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
