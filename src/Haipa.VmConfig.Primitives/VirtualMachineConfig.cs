using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Haipa.VmConfig
{
    public class VirtualMachineCpuConfig
    {
        public int? Count { get; set; }
    }

    public class VirtualMachineMemoryConfig
    {
        public int? Startup { get; set; }

        public int? Minimum { get; set; }

        public int? Maximum { get; set; }
    }

    public class VirtualMachineDriveConfig
    {
        public string Name { get; set; }
        public string ShareSlug { get; set; }
        public string DataStore { get; set; }

        public string Template { get; set; }

        public int? Size { get; set; }
        public VirtualMachineDriveType? Type { get; set; }
    }

    public enum VirtualMachineDriveType
    {
        // ReSharper disable InconsistentNaming
        VHD = 0,
        SharedVHD = 1,
        PHD = 2,
        DVD = 3,
        // ReSharper restore InconsistentNaming

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
        public string Slug { get; set; }
        public string DataStore { get; set; }

        public VirtualMachineCpuConfig Cpu { get; set; }

        public VirtualMachineMemoryConfig Memory { get; set; }

        public List<VirtualMachineDriveConfig> Drives { get; set; }

        public List<VirtualMachineNetworkAdapterConfig> NetworkAdapters { get; set; }

    }

    public class MachineImageConfig
    {
        public MachineImageSource Source { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
    }

    public enum MachineImageSource
    {
        Local,
        VagrantVM
    }

    public class MachineConfig
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Environment { get; set; }
        public string Project { get; set; }

        public MachineImageConfig Image { get; set; }

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
