using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Core.Enums
{
    public enum ReceiverConnectionStatus
    {
        Unknown      = -1,
        Online       = 0,
        Offline      = 1,
        Reconnecting = 2,
        Error        = 3
    }
}
