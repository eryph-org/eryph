using System;
using Eryph.ConfigModel.Machine;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class ValidateMachineConfigCommand
    {
        public MachineConfig Config { get; set; }
        public Guid MachineId { get; set; }
    }
}