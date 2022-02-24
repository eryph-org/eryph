using Eryph.Core;

namespace Eryph.Resources.Machines
{
    public class MachineData
    {
        [PrivateIdentifier]
        public string Name { get; set; }

        public MachineNetworkData[] Networks { get; set; }
    }
}