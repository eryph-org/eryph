using System;
using System.Collections.Generic;

namespace Haipa.Messages
{
    public class UpdateInventoryCommand
    {
        public string AgentName { get; set; }

        public List<MachineInfo> Inventory { get; set; }


    }
    

    public class VirtualMachineConvergedEvent
    {
        public Guid CorellationId { get; set; }
        public MachineInfo Inventory { get; set; }

    }

    public class VirtualMachineNetworkChangedEvent
    {
        public Guid MachineId { get; set; }

        public VirtualMachineNetworkInfo ChangedNetwork { get; set; }
        public VirtualMachineNetworkAdapterInfo ChangedAdapter { get; set; }

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

    public class GenerateIdCommand
    {
    }

    public class GenerateIdReply
    {
        public long GeneratedId { get; set; }

    }
}