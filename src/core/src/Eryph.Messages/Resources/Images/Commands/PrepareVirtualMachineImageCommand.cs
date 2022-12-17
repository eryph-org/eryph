using Eryph.ConfigModel;

namespace Eryph.Messages.Resources.Images.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class PrepareVirtualMachineImageCommand: IHostAgentCommand
    {
        public string Image { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
    }

    public class PrepareVirtualMachineImageResponse
    {
        public string RequestedImage { get; set; }

        public string ResolvedImage { get; set; }

    }
}