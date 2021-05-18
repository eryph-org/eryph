using System;
using Haipa.Messages.Operations.Commands;
using Haipa.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class ValidateMachineConfigCommand
    {
        public MachineConfig Config { get; set; }
        public Guid MachineId { get; set; }
    }
}