using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.VMAgent)]
    public class ConvergeVirtualMachineCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
    }
}