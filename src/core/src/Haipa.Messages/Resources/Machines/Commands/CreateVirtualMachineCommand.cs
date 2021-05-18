using System;
using Haipa.Messages.Operations.Commands;
using Haipa.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class CreateVirtualMachineCommand : IHostAgentCommand
    {
        public MachineConfig Config { get; set; }
        public Guid NewMachineId { get; set; }
        public string AgentName { get; set; }
        public long StorageId { get; set; }
    }
}