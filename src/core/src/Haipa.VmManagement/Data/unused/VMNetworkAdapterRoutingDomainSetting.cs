using System;

namespace Haipa.VmManagement.Data
{
    public sealed class VMNetworkAdapterRoutingDomainSetting
    {
        public Guid RoutingDomainID { get; private set; }

        public string RoutingDomainName { get; private set; }
        public int[] IsolationID { get; private set; }

        public string[] IsolationName { get; private set; }

    }
}