using System;

namespace Eryph.StateDb.Model;

public class NetworkRouterPort : VirtualNetworkPort
{
    public Guid RoutedNetworkId { get; set; }
    
    public VirtualNetwork RoutedNetwork { get; set; } = null!;
}
