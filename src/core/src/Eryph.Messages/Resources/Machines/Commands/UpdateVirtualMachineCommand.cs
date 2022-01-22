using System;
using Eryph.Messages.Operations.Commands;
using Eryph.Resources.Machines;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateVirtualMachineCommand : IHostAgentCommand
    {
        public MachineConfig Config { get; set; }

        public Guid VMId { get; set; }

        public long NewStorageId { get; set; }

        public VirtualMachineMetadata MachineMetadata { get; set; }
        public string AgentName { get; set; }
    }
}