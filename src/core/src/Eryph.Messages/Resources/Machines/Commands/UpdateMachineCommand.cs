using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateMachineCommand : IHasCorrelationId, IResourceCommand
    {
        public CatletConfig Config { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
        public Guid CorrelationId { get; set; }
        public Resource Resource { get; set; }
    }
}