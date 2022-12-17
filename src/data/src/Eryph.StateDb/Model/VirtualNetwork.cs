using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class VirtualNetwork : Resource
{
    public VirtualNetwork()
    {
        ResourceType = ResourceType.VirtualNetwork;
    }

    public string NetworkProvider { get; set; }

    public string IpNetwork { get; set; }
    
    public NetworkRouterPort RouterPort { get; set; }

    public virtual List<VirtualNetworkPort> NetworkPorts { get; set; }
    public virtual List<VirtualNetworkSubnet> Subnets { get; set; }
}