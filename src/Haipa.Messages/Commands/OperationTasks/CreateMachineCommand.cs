using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{

    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateMachineCommand : OperationTaskCommand
    {
        public Guid CorrelationId { get; set; }
        public MachineConfig Config { get; set; }
    }
}