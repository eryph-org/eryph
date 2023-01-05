using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateVCatletCommand : IHostAgentCommand
    {
        public CatletConfig Config { get; set; }

        public Guid VMId { get; set; }

        public long NewStorageId { get; set; }

        public VirtualCatletMetadata MachineMetadata { get; set; }

        public MachineNetworkSettings[] MachineNetworkSettings { get; set; }


        [PrivateIdentifier]
        public string AgentName { get; set; }
    }
}