using Eryph.ConfigModel;
using Eryph.Core.Network;

namespace Eryph.Resources.Machines
{
    public class VMHostMachineData : MachineData
    {

        public NetworkProvidersConfiguration NetworkProviderConfiguration { get; set; }
    }
}