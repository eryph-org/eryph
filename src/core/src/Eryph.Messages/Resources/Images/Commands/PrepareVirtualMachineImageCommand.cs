using Eryph.Messages.Operations.Commands;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Images.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class PrepareVirtualMachineImageCommand: IHostAgentCommand
    {
        public MachineImageConfig ImageConfig { get; set; }
        public string AgentName { get; set; }
    }
}