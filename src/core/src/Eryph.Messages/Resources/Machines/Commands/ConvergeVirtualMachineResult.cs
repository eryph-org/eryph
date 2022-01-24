using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Machines.Commands
{
    public class ConvergeVirtualMachineResult
    {
        public VirtualMachineMetadata MachineMetadata { get; set; }
        public VirtualMachineData Inventory { get; set; }
    }
}