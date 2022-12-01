using System;

namespace Eryph.StateDb.Model;

public class VirtualNetworkSubnet : Subnet
{
    public Guid NetworkId { get; set; }
    public VirtualNetwork Network { get; set; }

}