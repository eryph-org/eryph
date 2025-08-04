using System;
using Eryph.VmManagement.Data;

namespace Eryph.Modules.HostAgent.Inventory;

internal class VirtualMachineStateChangedEvent
{
    public Guid VmId { get; set; }

    public VirtualMachineState? State { get; set; }

    public TimeSpan UpTime { get; set; }
    
    public DateTimeOffset Timestamp { get; set; }
}
