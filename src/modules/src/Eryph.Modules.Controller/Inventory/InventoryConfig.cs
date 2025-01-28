using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Inventory;

internal class InventoryConfig
{
    public TimeSpan InventoryInterval { get; init; } = TimeSpan.FromMinutes(10);
}