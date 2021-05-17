using Haipa.Messages.Operations.Commands;
using Haipa.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class PlaceVirtualMachineCommand : OperationTaskCommand
    {
        public MachineConfig Config { get; set; }
    }
}