using System;
using System.Collections.Generic;

namespace HyperVPlus.Messages
{
    public class UpdateInventoryCommand
    {
        public string AgentName { get; set; }

        public List<VmInventoryInfo> Inventory { get; set; }


    }

    public class VirtualMachineConvergedEvent
    {
        public Guid CorellationId { get; set; }
        public VmInventoryInfo Inventory { get; set; }

    }

    public class OperationCompletedEvent
    {
        public Guid OperationId { get; set; }

    }

    public class OperationFailedEvent
    {
        public Guid OperationId { get; set; }
        public string ErrorMessage { get; set; }
    }
}