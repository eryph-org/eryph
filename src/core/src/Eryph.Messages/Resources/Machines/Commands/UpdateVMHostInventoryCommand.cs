using System.Collections.Generic;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateVMHostInventoryCommand
    {
        public VMHostMachineData HostInventory { get; set; }

        public List<VirtualMachineData> VMInventory { get; set; }
    }
}