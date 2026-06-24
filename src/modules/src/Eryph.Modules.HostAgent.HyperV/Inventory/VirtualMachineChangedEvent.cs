using System;

namespace Eryph.Modules.HostAgent.Inventory;

internal class VirtualMachineChangedEvent
{
    public required Guid VmId { get; set; }
}
