using Eryph.ConfigModel.Machine;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class PlaceVirtualMachineCommand
    {
        public MachineConfig Config { get; set; }
    }
}