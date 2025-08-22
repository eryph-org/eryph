using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class CreateCatletVMCommand : IHostAgentCommand
    {
        public CatletConfig Config { get; set; }

        public Guid CatletId { get; set; }

        public Guid MetadataId { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }

        public long StorageId { get; set; }
    }
}