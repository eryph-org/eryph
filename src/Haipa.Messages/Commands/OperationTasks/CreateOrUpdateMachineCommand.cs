using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{

    [Message(MessageOwner.Controllers)]
    public class CreateOrUpdateMachineCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
    }
}