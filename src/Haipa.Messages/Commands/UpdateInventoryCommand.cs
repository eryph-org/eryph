using System.Collections.Generic;
using Haipa.Messages.Events;

namespace Haipa.Messages.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateInventoryCommand
    {
        public string AgentName { get; set; }

        public List<VirtualMachineInfo> Inventory { get; set; }


    }

    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateVMHostInventoryCommand
    {
        public VMHostMachineInfo HostInventory { get; set; }

        public List<VirtualMachineInfo> VMInventory { get; set; }


    }
}