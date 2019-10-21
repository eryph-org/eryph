using System.Collections.Generic;
using Haipa.Messages.Events;

namespace Haipa.Messages.Commands
{
    [Message(MessageOwner.Controllers)]
    public class UpdateInventoryCommand
    {
        public string AgentName { get; set; }

        public List<MachineInfo> Inventory { get; set; }


    }
}