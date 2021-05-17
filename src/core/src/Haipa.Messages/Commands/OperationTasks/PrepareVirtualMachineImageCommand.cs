using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class PrepareVirtualMachineImageCommand : OperationTaskCommand, IHostAgentCommand
    {
        public string AgentName { get; set; }

        public MachineImageConfig ImageConfig { get; set; }

    }
}