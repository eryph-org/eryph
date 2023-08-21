using System;
using Eryph.VmManagement.Data;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    internal class VirtualMachineStateChangedEvent
    {
        public Guid VmId { get; set; }
        public VirtualMachineState State { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}