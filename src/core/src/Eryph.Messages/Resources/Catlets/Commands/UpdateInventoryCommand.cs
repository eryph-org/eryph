using System;
using Eryph.ConfigModel;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateInventoryCommand
    {
        [PrivateIdentifier]
        public string AgentName { get; set; }

        public VirtualMachineData Inventory { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}