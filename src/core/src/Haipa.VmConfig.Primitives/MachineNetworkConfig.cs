using System.Collections.Generic;

namespace Haipa.VmConfig
{
    public class MachineNetworkConfig
    {
        public string AdapterName { get; set; }

        public List<MachineSubnetConfig> Subnets { get; set; }
    }
}