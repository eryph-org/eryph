using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class ConvergeVirtualMachineCommand : OperationTaskCommand, IHostAgentCommand
    {
        public MachineConfig Config { get; set; }
        public string AgentName { get; set; }
    }
}