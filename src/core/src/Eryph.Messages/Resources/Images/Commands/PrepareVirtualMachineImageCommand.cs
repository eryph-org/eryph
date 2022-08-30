using Eryph.Core;

namespace Eryph.Messages.Resources.Images.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class PrepareVirtualMachineImageCommand: IHostAgentCommand
    {
        public string Image { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
    }
}