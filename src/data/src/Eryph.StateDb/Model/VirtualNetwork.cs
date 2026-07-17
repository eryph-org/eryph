using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class VirtualNetwork : Resource, ISiteBound
{
    public VirtualNetwork()
    {
        ResourceType = ResourceType.VirtualNetwork;
    }

    public required Guid SiteId { get; set; }

    public Site Site { get; set; } = null!;

    public required string NetworkProvider { get; set; }

    public string? IpNetwork { get; set; }

    public NetworkRouterPort? RouterPort { get; set; } = null!;

    public List<VirtualNetworkPort> NetworkPorts { get; set; } = null!;

    public List<VirtualNetworkSubnet> Subnets { get; set; } = null!;
}
