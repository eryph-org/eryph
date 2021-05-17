using Haipa.Messages.Operations.Commands;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Images.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class PrepareVirtualMachineImageCommand : OperationTaskCommand, IHostAgentCommand
    {
        public string AgentName { get; set; }

        public MachineImageConfig ImageConfig { get; set; }

    }
}