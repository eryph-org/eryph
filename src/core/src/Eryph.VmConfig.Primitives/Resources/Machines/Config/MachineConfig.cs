using System.Collections.Generic;
using Eryph.Core;
using JetBrains.Annotations;

namespace Eryph.Resources.Machines.Config
{
    [PublicAPI]
    public class MachineConfig
    {
        [PrivateIdentifier]
        public string Name { get; set; }
        public string Environment { get; set; }
        public string Project { get; set; }

        public MachineImageConfig Image { get; set; }

        public VirtualMachineConfig VM { get; set; }

        public List<MachineNetworkConfig> Networks { get; set; }

        public VirtualMachineProvisioningConfig Provisioning { get; set; }
    }
}