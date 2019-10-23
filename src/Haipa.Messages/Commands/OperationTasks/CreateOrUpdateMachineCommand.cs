using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{

    [SendMessageTo(MessageRecipient.Controllers)]
    public class CreateOrUpdateMachineCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
    }
}