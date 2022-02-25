using System;
using Eryph.Core;
using Eryph.Messages.Operations.Commands;
using Eryph.Resources;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateMachineCommand : IHasCorrelationId, IResourceCommand
    {
        public MachineConfig Config { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
        public Guid CorrelationId { get; set; }
        public Resource Resource { get; set; }
    }
}