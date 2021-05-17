using System;

namespace Haipa.Messages.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class VirtualMachineNetworkChangedEvent
    {
        public Guid VMId { get; set; }

        public MachineNetworkInfo ChangedNetwork { get; set; }
        public VirtualMachineNetworkAdapterInfo ChangedAdapter { get; set; }

    }
}