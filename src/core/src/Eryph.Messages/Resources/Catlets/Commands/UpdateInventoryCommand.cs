using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateInventoryCommand
    {
        [PrivateIdentifier]
        public string AgentName { get; set; }

        public List<VirtualMachineData> Inventory { get; set; }
        public Guid TenantId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}