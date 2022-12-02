using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public abstract class VirtualNetworkPort: NetworkPort
    {

        public Guid NetworkId { get; set; }
        public virtual VirtualNetwork Network { get; set; }

    }

    public class NetworkRouterPort : VirtualNetworkPort
    {

        public Guid RoutedNetworkId { get; set; }
        public virtual VirtualNetwork RoutedNetwork { get; set; }

    }


    public abstract class NetworkPort
    {
        public Guid Id { get; set; }
        public string MacAddress { get; set; }

        public string Name { get; set; }
        public virtual List<IpAssignment> IpAssignments { get; set; }

    }

    public class ProviderNetworkPort : NetworkPort
    {
        public string ProviderName { get; set; }

    }

}