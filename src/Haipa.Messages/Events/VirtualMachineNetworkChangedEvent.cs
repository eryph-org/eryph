using System;

namespace Haipa.Messages.Events
{
    [SubscribeEvent(MessageSubscribers.ControllerModules)]
    [Message(MessageOwner.VMAgent)]
    public class VirtualMachineNetworkChangedEvent
    {
        public Guid MachineId { get; set; }

        public VirtualMachineNetworkInfo ChangedNetwork { get; set; }
        public VirtualMachineNetworkAdapterInfo ChangedAdapter { get; set; }

    }
}