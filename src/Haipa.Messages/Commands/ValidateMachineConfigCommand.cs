using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class ValidateMachineConfigCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
        public Guid MachineId { get; set; }
    }
}