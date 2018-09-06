using System;

namespace HyperVPlus.Messages
{
    public class InventoryRequestedEvent
    {

    }

    public class VirtualMachineStartRequestedEvent
    {
        public Guid VirtualMachineId { get; set; }
        public Guid CorrelationId { get; set; }
    }
}