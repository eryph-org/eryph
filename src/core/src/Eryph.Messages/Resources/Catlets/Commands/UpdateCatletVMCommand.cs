using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateCatletVMCommand : IHostAgentCommand, IVMCommand, IHasResource
    {
        public CatletConfig Config { get; set; }

        public Guid VMId { get; set; }

        public Guid CatletId { get; set; }


        public long NewStorageId { get; set; }

        public VirtualCatletMetadata MachineMetadata { get; set; }

        public MachineNetworkSettings[] MachineNetworkSettings { get; set; }


        [PrivateIdentifier]
        public string AgentName { get; set; }


        public Resource Resource => new(ResourceType.Catlet, CatletId);
    }
}