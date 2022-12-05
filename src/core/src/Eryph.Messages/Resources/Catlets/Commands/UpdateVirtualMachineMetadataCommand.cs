using System;
using Eryph.Core;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateVirtualMachineMetadataCommand :  IHostAgentCommand
    {
        public Guid NewMetadataId { get; set; }
        public Guid CurrentMetadataId { get; set; }
        public Guid VMId { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
    }
}