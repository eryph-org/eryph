using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class CreateVirtualMachineCommand : IHostAgentCommand
    {
        public CatletConfig Config { get; set; }
        public Guid NewMachineId { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
        public long StorageId { get; set; }
    }
}