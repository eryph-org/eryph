using Haipa.Messages.Operations.Commands;
using Haipa.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Images.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class PrepareVirtualMachineImageCommand : OperationTaskCommand, IHostAgentCommand
    {
        public MachineImageConfig ImageConfig { get; set; }
        public string AgentName { get; set; }
    }
}