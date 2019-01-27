using System;

namespace Haipa.Messages
{
    public class MachineInfo
    {

        public string Name { get; set; }

        public Guid MachineId { get; set; }
        
        public VmStatus Status { get; set; }

        public VirtualMachineNetworkAdapterInfo[] NetworkAdapters { get; set; }
        public VirtualMachineNetworkInfo[] Networks { get; set; }

    }

    public class VirtualMachineNetworkAdapterInfo
    {
        public string AdapterName { get; set; }
        public string VirtualSwitchName { get; set; }
        public ushort VLanId { get; set; }
        public string MACAddress { get; set; }        
    }

    public class VirtualMachineNetworkInfo
    {
        public string AdapterName { get; set; }       
        
        public string[] Subnets { get; set; }
        public string[] IPAddresses { get; set; }

        public string[] DnsServers { get; set; }

        public string[] DefaultGateways { get; set; }

        public bool DhcpEnabled { get; set; }
    }

    public enum NetworkType
    {
        Bridged,
        Private
    }

    public enum VmStatus
    {
        Stopped,
        Running,
        Pending,
        Error,
    }
}