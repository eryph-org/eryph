using Eryph.Core;

namespace Eryph.Resources.Machines
{
    public class VMHostMachineData : MachineData
    {
        [PrivateIdentifier]
        public string HardwareId { get; set; }

    }
}