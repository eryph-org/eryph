using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Resources.Disks;

namespace Eryph.Messages.Resources.Disks;

public class UpdateDiskInventoryCommand
{
    public string AgentName { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public IList<DiskInfo> Inventory { get; init; }
}
