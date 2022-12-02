using System;

namespace Eryph.StateDb.Model;

public class VirtualNetworkSubnet : Subnet
{
    public Guid NetworkId { get; set; }
    public VirtualNetwork Network { get; set; }

    public int DhcpLeaseTime { get; set; }
    public int MTU { get; set; }
    public string DnsServersV4 { get; set; }

}