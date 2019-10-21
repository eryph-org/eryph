using System;
using Haipa.VmManagement.Data;

namespace Haipa.Modules.VmHostAgent
{
    internal class VirtualMachineStateChangedEvent
    {
        public Guid VmId { get; set; }
        public VirtualMachineState State { get; set; }
    }
}