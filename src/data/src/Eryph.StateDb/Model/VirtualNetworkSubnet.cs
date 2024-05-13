using System;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class VirtualNetworkSubnet : Subnet
{
    public Guid NetworkId { get; set; }

    public VirtualNetwork Network { get; set; } = null!;

    public int DhcpLeaseTime { get; set; }
    
    public int MTU { get; set; }
    
    public string? DnsServersV4 { get; set; }
}