using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Resources;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateCatletVMCommand : IHostAgentCommand, IVMCommand, IHasResource
    {
        public CatletConfig Config { get; set; }

        public Guid VmId { get; set; }

        public Guid CatletId { get; set; }

        public Guid MetadataId { get; set; }

        public long NewStorageId { get; set; }

        public MachineNetworkSettings[] MachineNetworkSettings { get; set; }

        public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }

        public Resource Resource => new(ResourceType.Catlet, CatletId);
    }
}