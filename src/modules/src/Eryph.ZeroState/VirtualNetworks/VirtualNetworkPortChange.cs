using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState.VirtualNetworks;

internal class VirtualNetworkPortChange
{
    public List<Guid> ProjectIds { get; set; } = new();
}
