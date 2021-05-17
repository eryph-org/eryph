using System.Collections.Generic;
using Haipa.Resources.Machines;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateVMHostInventoryCommand
    {
        public VMHostMachineData HostInventory { get; set; }

        public List<VirtualMachineData> VMInventory { get; set; }
    }
}