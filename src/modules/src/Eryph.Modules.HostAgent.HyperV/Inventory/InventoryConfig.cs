using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.HostAgent.Inventory;

public class InventoryConfig
{
    public TimeSpan DiskEventDelay { get; set; } = TimeSpan.FromSeconds(5);
}
