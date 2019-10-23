using System;

namespace Haipa.Messages.Operations
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class AttachMachineToOperationCommand
    {
        public Guid OperationId { get; set; }
        public Guid MachineId { get; set; }
        public string AgentName { get; set; }

    }
}