using Eryph.ConfigModel;

namespace Eryph.Resources.Machines
{
    public class MachineNetworkData
    {

        public string NetworkProviderName { get; set; }
        public string PortName { get; set; }

        [PrivateIdentifier]
        public string AdapterName { get; set; }

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