using System;
using Eryph.Messages.Operations.Commands;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateVirtualMachineMetadataCommand :  IHostAgentCommand
    {
        public Guid NewMetadataId { get; set; }
        public Guid CurrentMetadataId { get; set; }
        public Guid VMId { get; set; }

        public string AgentName { get; set; }
    }
}