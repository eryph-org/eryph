using System;
using Haipa.Messages.Operations.Commands;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateVirtualMachineMetadataCommand : OperationTaskCommand, IHostAgentCommand
    {
        public Guid NewMetadataId { get; set; }
        public Guid CurrentMetadataId { get; set; }
        public Guid VMId { get; set; }

        public string AgentName { get; set; }
    }
}