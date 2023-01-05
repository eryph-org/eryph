using System;

namespace Eryph.Messages.Resources.Networks.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class UpdateNetworksCommand : IHasCorrelationId
    {
        public Guid[] Projects { get; set; }
        public Guid CorrelationId { get; set; }
    }
}