using Eryph.Core;

namespace Eryph.Resources.Machines
{
    public class MachineNetworkData
    {

        [PrivateIdentifier] 
        public string Name { get; set; }

        [PrivateIdentifier]
        public string[] AdapterNames { get; set; }

        public string[] Subnets { get; set; }

        [PrivateIdentifier]
        public string[] IPAddresses { get; set; }

        [PrivateIdentifier]
        public string[] DnsServers { get; set; }

        [PrivateIdentifier]
        public string[] DefaultGateways { get; set; }

        public bool DhcpEnabled { get; set; }
    }
}