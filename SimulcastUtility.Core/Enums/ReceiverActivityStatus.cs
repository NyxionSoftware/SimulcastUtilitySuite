using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Enums
{
    public enum ReceiverActivityStatus
    {
        Unknown = -1,
        Idle = 0,
        Loading = 1,
        Editing = 2,
        Transmitting = 3
    }
}
