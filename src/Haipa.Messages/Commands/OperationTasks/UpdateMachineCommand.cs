using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateMachineCommand : OperationTaskCommand
    {
        public Guid MachineId { get; set; }
        public MachineConfig Config { get; set; }
        public string AgentName { get; set; }
    }
}