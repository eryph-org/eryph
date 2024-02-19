using System;
using System.Collections.Generic;
using Eryph.Resources.Machines;
using JetBrains.Annotations;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class UpdateVMHostInventoryCommand
    {
        public VMHostMachineData HostInventory { get; set; }

        public List<VirtualMachineData> VMInventory { get; set; }

        public Guid TenantId { get; set; }
    }
}