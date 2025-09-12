using System;
using Eryph.ConfigModel;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateCatletMetadataCommand :  IHostAgentCommand, IVMCommand, IHasResource
    {
        public Guid NewMetadataId { get; set; }
        public Guid CurrentMetadataId { get; set; }
        public Guid VmId { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }

        public Guid CatletId { get; set; }

        public Resource Resource => new(ResourceType.Catlet, CatletId);
    }
}