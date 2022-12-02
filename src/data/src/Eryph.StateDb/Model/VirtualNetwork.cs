using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class VirtualNetwork : Resource
{
    public string NetworkProvider { get; set; }

    public string IpNetwork { get; set; }
    
    public NetworkRouterPort RouterPort { get; set; }

    public virtual List<VirtualNetworkPort> NetworkPorts { get; set; }
    public virtual List<VirtualNetworkSubnet> Subnets { get; set; }
}