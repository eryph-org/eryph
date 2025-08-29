using System;

namespace Eryph.Modules.HostAgent.Inventory;

internal class VirtualMachineRemovedEvent
{
    public required Guid VmId { get; set; }
}
