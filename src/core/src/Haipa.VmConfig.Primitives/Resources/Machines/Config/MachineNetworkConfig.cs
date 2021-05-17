using System.Collections.Generic;

namespace Haipa.Primitives.Resources.Machines.Config
{
    public class MachineNetworkConfig
    {
        public string AdapterName { get; set; }

        public List<MachineSubnetConfig> Subnets { get; set; }
    }
}