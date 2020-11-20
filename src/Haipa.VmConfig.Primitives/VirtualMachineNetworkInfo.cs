namespace Haipa.Messages.Events
{
    public class MachineNetworkInfo
    {
        public string Name { get; set; }       
        
        public string[] Subnets { get; set; }
        public string[] IPAddresses { get; set; }

        public string[] DnsServers { get; set; }

        public string[] DefaultGateways { get; set; }

        public bool DhcpEnabled { get; set; }
    }
}