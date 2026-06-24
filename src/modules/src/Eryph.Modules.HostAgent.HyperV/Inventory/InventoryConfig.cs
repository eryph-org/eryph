using System;

namespace Eryph.Modules.HostAgent.Inventory;

public class InventoryConfig
{
    public TimeSpan DiskEventDelay { get; set; } = TimeSpan.FromSeconds(5);
}
