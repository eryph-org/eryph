using Eryph.Messages.Operations.Commands;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class StopMachineCommand : IResourceCommand
    {
        public Resource Resource { get; set; }
    }
}