using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class PlaceVirtualMachineCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
    }
}