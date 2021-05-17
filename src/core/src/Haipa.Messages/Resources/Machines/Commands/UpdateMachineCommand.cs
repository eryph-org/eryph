using System;
using Haipa.Messages.Operations.Commands;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
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