using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    public class ConvergeVirtualCatletResult
    {
        public VirtualCatletMetadata MachineMetadata { get; set; }
        public VirtualMachineData Inventory { get; set; }
    }
}