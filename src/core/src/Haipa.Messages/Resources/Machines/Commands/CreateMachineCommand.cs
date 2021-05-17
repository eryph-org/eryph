using System;
using Haipa.Messages.Operations.Commands;
using Haipa.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateMachineCommand : IHasCorrelationId
    {
        public MachineConfig Config { get; set; }
        public Guid CorrelationId { get; set; }
    }
}