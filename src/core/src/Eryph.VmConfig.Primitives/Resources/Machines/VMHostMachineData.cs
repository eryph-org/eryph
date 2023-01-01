using Eryph.ConfigModel;
using Eryph.Core.Network;

namespace Eryph.Resources.Machines
{
    public class VMHostMachineData : MachineData
    {
        [PrivateIdentifier]
        public string HardwareId { get; set; }

        public NetworkProvidersConfiguration NetworkProviderConfiguration { get; set; }
    }
}