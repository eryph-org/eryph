using System;
using Eryph.Core;
using Eryph.Messages.Operations.Commands;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class CreateVirtualMachineCommand : IHostAgentCommand
    {
        public MachineConfig Config { get; set; }
        public Guid NewMachineId { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
        public long StorageId { get; set; }
    }
}