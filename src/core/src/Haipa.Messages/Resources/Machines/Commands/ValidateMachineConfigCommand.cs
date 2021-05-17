using Haipa.Messages.Operations.Commands;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class ValidateMachineConfigCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
        public long MachineId { get; set; }
    }
}