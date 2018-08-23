using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyperVPlus.Messages
{
    public class VirtualMachineCpuConfig
    {
        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class VirtualMachineMemoryConfig
    {
        [JsonProperty("startup")]
        public int Startup { get; set; }

        [JsonProperty("minimum")]
        public int Minimum { get; set; }

        [JsonProperty("maximum")]
        public int Maximum { get; set; }
    }

    public class VirtualMachineDiskConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }


        [JsonProperty("template")]
        public string Template { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }
    }

    public class VirtualMachineSubnetConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class VirtualMachineNetworkConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("switch")]
        public string SwitchName { get; set; }

        [JsonProperty("subnets")]
        public List<VirtualMachineSubnetConfig> Subnets { get; set; }
    }

    public class HostConfig
    {
        [JsonProperty("hostname")]
        public string Hostname { get; set; }

    }

    public class VirtualMachineConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("cpu")]
        public VirtualMachineCpuConfig Cpu { get; set; }

        [JsonProperty("memory")]
        public VirtualMachineMemoryConfig Memory { get; set; }

        [JsonProperty("disks")]
        public List<VirtualMachineDiskConfig> Disks { get; set; }

        [JsonProperty("networks")]
        public List<VirtualMachineNetworkConfig> Networks { get; set; }

        [JsonProperty("provisioning")]
        public VirtualMachineProvisioningConfig Provisioning { get; set; }

    }

    public class VirtualMachineProvisioningConfig
    {
        [JsonProperty("userdata")]
        public JObject UserData { get; set; }

    }

    public class ConfigEntry
    {
        [JsonProperty("host")]
        public HostConfig Host { get; set; }
        [JsonProperty("vm")]
        public VirtualMachineConfig VirtualMachine { get; set; }

    }

    public class Config
    {
        [JsonProperty("vms")]
        public ConfigEntry[] Configurations { get; set; }

    }
}
