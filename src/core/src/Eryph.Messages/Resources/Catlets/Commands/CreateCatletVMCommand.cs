using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class CreateCatletVMCommand : IHostAgentCommand
    {
        public CatletConfig Config { get; set; }

        public CatletConfig BredConfig { get; set; }

        public Guid NewMachineId { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }

        public long StorageId { get; set; }
    }
}