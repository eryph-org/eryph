using System;

namespace Eryph.Modules.Controller.Inventory;

internal class InventoryConfig
{
    public TimeSpan InventoryInterval { get; init; } = TimeSpan.FromMinutes(10);
}
