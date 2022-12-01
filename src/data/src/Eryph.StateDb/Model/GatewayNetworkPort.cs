using System;

namespace Eryph.StateDb.Model;

public class GatewayNetworkPort : VirtualNetworkPort
{

    public Guid GatewayNetworkId { get; set; }
    public virtual VirtualNetwork GatewayNetwork { get; set; }


}