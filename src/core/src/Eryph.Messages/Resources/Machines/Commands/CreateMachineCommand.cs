using System;
using Eryph.Messages.Operations.Commands;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateMachineCommand : IHasCorrelationId
    {
        public MachineConfig Config { get; set; }
        public Guid CorrelationId { get; set; }
    }
}