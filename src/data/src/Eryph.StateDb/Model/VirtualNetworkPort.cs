using System;

namespace Eryph.StateDb.Model;

public abstract class VirtualNetworkPort: NetworkPort
{
    public Guid NetworkId { get; set; }

    public VirtualNetwork Network { get; set; } = null!;

    public Guid? FloatingPortId { get; set; }

    public FloatingNetworkPort? FloatingPort { get; set; }
}
