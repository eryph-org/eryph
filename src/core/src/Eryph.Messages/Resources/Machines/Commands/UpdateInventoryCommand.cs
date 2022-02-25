using System.Collections.Generic;
using Eryph.Core;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateInventoryCommand
    {
        [PrivateIdentifier]
        public string AgentName { get; set; }

        public List<VirtualMachineData> Inventory { get; set; }
    }
}