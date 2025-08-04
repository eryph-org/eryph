using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.HostAgent.Inventory;

internal class VirtualMachineChangedEvent
{
    public required Guid VmId { get; set; }
}