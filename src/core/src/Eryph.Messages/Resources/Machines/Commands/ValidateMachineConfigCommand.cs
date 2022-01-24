using System;
using Eryph.Messages.Operations.Commands;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class ValidateMachineConfigCommand
    {
        public MachineConfig Config { get; set; }
        public Guid MachineId { get; set; }
    }
}