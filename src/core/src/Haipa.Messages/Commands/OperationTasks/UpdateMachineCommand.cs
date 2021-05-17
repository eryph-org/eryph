using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateMachineCommand : OperationTaskCommand, IHasCorrelationId
    {
        public Guid CorrelationId { get; set; }
        public long MachineId { get; set; }
        public MachineConfig Config { get; set; }
        public string AgentName { get; set; }
    }
}