using System;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class VirtualMachineNetworkChangedEvent
    {
        public Guid VMId { get; set; }

        public MachineNetworkData ChangedNetwork { get; set; }
        public VirtualMachineNetworkAdapterData ChangedAdapter { get; set; }
    }
}