using System;

namespace HyperVPlus.Messages
{
    public class ConvergeVirtualMachineRequestedEvent
    {
        public Guid CorellationId { get; set; }
        public VirtualMachineConfig Config { get; set; }

    }
}