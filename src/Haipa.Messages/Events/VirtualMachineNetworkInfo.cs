namespace Haipa.Messages.Events
{
    public class VirtualMachineNetworkInfo
    {
        public string AdapterName { get; set; }       
        
        public string[] Subnets { get; set; }
        public string[] IPAddresses { get; set; }

        public string[] DnsServers { get; set; }

        public string[] DefaultGateways { get; set; }

        public bool DhcpEnabled { get; set; }
    }
}