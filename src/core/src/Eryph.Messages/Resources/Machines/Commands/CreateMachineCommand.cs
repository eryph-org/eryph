using System;
using Eryph.ConfigModel.Machine;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateMachineCommand : IHasCorrelationId
    {
        public MachineConfig Config { get; set; }
        public Guid CorrelationId { get; set; }
    }
}