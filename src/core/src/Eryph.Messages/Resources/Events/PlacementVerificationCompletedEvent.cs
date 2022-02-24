using System;
using Eryph.Core;

namespace Eryph.Messages.Resources.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class PlacementVerificationCompletedEvent
    {
        public Guid CorrelationId { get; set; }

        [PrivateIdentifier]
        public string AgentName { get; set; }
        public bool Confirmed { get; set; }
    }
}