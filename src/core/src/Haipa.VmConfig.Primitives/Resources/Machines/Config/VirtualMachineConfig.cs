using System.Collections.Generic;

namespace Haipa.Resources.Machines.Config
{
    public class VirtualMachineConfig
    {
        public string Slug { get; set; }
        public string DataStore { get; set; }

        public VirtualMachineCpuConfig Cpu { get; set; }

        public VirtualMachineMemoryConfig Memory { get; set; }

        public List<VirtualMachineDriveConfig> Drives { get; set; }

        public List<VirtualMachineNetworkAdapterConfig> NetworkAdapters { get; set; }
    }
}