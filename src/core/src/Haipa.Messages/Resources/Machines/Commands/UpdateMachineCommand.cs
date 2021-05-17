using System;
using Haipa.Messages.Operations.Commands;
using Haipa.Resources;
using Haipa.Resources.Machines.Config;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateMachineCommand : IHasCorrelationId, IResourceCommand
    {
        public MachineConfig Config { get; set; }
        public string AgentName { get; set; }
        public Guid CorrelationId { get; set; }
        public Resource Resource { get; set; }
    }
}