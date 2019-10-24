using System;

namespace Haipa.Messages.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class VirtualMachineNetworkChangedEvent
    {
        public Guid MachineId { get; set; }

        public VirtualMachineNetworkInfo ChangedNetwork { get; set; }
        public VirtualMachineNetworkAdapterInfo ChangedAdapter { get; set; }

    }
}