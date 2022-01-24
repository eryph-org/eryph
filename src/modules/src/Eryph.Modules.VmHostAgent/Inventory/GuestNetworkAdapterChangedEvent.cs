using System;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    internal class GuestNetworkAdapterChangedEvent
    {
        public Guid VmId { get; set; }
        public string AdapterId { get; set; }

        public string[] IPAddresses { get; set; }
        public string[] Netmasks { get; set; }
        public string[] DnsServers { get; set; }
        public string[] DefaultGateways { get; set; }
        public bool DhcpEnabled { get; set; }
    }
}