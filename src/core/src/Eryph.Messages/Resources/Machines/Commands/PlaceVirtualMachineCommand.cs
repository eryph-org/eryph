using Eryph.Messages.Operations.Commands;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class PlaceVirtualMachineCommand
    {
        public MachineConfig Config { get; set; }
    }
}