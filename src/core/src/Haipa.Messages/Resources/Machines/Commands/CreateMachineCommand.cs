using System;
using Haipa.Messages.Operations.Commands;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{

    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateMachineCommand : OperationTaskCommand, IHasCorrelationId
    {
        public Guid CorrelationId { get; set; }
        public MachineConfig Config { get; set; }
    }
}