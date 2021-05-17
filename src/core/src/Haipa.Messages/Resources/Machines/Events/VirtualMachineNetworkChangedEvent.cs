using System;
using Haipa.Resources.Machines;

namespace Haipa.Messages.Resources.Machines.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class VirtualMachineNetworkChangedEvent
    {
        public Guid VMId { get; set; }

        public MachineNetworkData ChangedNetwork { get; set; }
        public VirtualMachineNetworkAdapterData ChangedAdapter { get; set; }
    }
}