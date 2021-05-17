using System;
using Haipa.Messages.Operations.Commands;
using Haipa.Resources.Machines;
using Haipa.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateVirtualMachineCommand : OperationTaskCommand, IHostAgentCommand
    {
        public MachineConfig Config { get; set; }

        public Guid VMId { get; set; }

        public long NewStorageId { get; set; }

        public VirtualMachineMetadata MachineMetadata { get; set; }
        public string AgentName { get; set; }
    }
}