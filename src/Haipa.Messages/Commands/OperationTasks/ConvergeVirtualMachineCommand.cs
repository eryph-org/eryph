using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [Message(MessageOwner.VMAgent)]
    public class ConvergeVirtualMachineCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
    }
}