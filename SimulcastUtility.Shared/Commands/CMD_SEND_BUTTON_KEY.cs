using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Shared.Commands
{
    public class CMD_SEND_BUTTON_KEY : CMD_STB_MESSAGE
    {
        public CMD_SEND_BUTTON_KEY(string Key)
        {
            Id = CommandIdGenerator.Next();
            ApiKey = "dca15ceb-39c9-49f8-a0a6-a85c7402af6e";
            Command = "CMD_SEND_BUTTON_KEY";
            Description = "Send KEY button press to STB.";
            Payload = new CMD_STB_MESSAGE_PAYLOAD(buttonKey: Key);
        }
    }
}
