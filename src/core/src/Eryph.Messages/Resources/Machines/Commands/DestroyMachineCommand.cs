using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Commands;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyMachineCommand : IResourceCommand
    {
        public Resource Resource { get; set; }
    }


    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyVirtualDiskCommand : IResourceCommand
    {
        public Resource Resource { get; set; }
    }
}