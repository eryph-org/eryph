using System.Collections.Generic;
using Haipa.Messages.Events;

namespace Haipa.Messages.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateInventoryCommand
    {
        public string AgentName { get; set; }

        public List<MachineInfo> Inventory { get; set; }


    }
}