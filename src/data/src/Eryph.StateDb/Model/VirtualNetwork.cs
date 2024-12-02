using System.Collections.Generic;
using Eryph.Resources;
using JetBrains.Annotations;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class VirtualNetwork : Resource
{
    public VirtualNetwork()
    {
        ResourceType = ResourceType.VirtualNetwork;
    }

    public required string NetworkProvider { get; set; }

    public string? IpNetwork { get; set; }
    
    public NetworkRouterPort? RouterPort { get; set; } = null!;

    public List<VirtualNetworkPort> NetworkPorts { get; set; } = null!;
    
    public List<VirtualNetworkSubnet> Subnets { get; set; } = null!;
}
