using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    public class ConvergeCatletResult
    {
        public CatletMetadata MachineMetadata { get; set; }
        public VirtualMachineData Inventory { get; set; }
    }
}