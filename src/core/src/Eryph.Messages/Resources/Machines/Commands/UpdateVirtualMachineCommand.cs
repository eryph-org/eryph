using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class UpdateVirtualMachineCommand : IHostAgentCommand
    {
        public CatletConfig Config { get; set; }

        public Guid VMId { get; set; }

        public long NewStorageId { get; set; }

        public VirtualMachineMetadata MachineMetadata { get; set; }

        public MachineNetworkSettings[] MachineNetworkSettings { get; set; }


        [PrivateIdentifier]
        public string AgentName { get; set; }
    }
}