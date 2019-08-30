using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Haipa.VmConfig
{
    public class VirtualMachineCpuConfig
    {
        public int Count { get; set; }
    }

    public class VirtualMachineMemoryConfig
    {
        public int Startup { get; set; }

        public int Minimum { get; set; }

        public int Maximum { get; set; }
    }

    public class VirtualMachineDiskConfig
    {
        public string Name { get; set; }

        public string Template { get; set; }

        public int Size { get; set; }
    }

    public class MachineSubnetConfig
    {
        public string Type { get; set; }
    }

    public class VirtualMachineNetworkAdapterConfig
    {
        public string Name { get; set; }

        public string SwitchName { get; set; }

        public string MacAddress { get; set; }

    }

    public class MachineNetworkConfig
    {
        public string AdapterName { get; set; }

        public List<MachineSubnetConfig> Subnets { get; set; }
    }

    public class HostConfig
    {
        public string Hostname { get; set; }

    }

    public class VirtualMachineConfig
    {
        public VirtualMachineCpuConfig Cpu { get; set; }

        public VirtualMachineMemoryConfig Memory { get; set; }

        public List<VirtualMachineDiskConfig> Disks { get; set; }

        public List<VirtualMachineNetworkAdapterConfig> NetworkAdapters { get; set; }

    }

    public class MachineConfig
    {
        public string Name { get; set; }
        public string Id { get; set; }

        public VirtualMachineConfig VM { get; set; }

        public List<MachineNetworkConfig> Networks { get; set; }

        public VirtualMachineProvisioningConfig Provisioning { get; set; }

    }

    public class VirtualMachineProvisioningConfig
    {
        public string Hostname { get; set; }

        public JObject UserData { get; set; }

        public ProvisioningMethod Method { get; set; }
    }

    public enum ProvisioningMethod
    {
        None = 0,
        CloudInit = 1

    }

    public class ConfigEntry
    {
        public HostConfig Host { get; set; }
        public MachineConfig VM { get; set; }

    }

    public class Config
    {
        public ConfigEntry[] Configurations { get; set; }

    }

    
}
