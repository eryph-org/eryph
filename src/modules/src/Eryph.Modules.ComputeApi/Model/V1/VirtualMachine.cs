using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class VirtualMachine : Machine
    {
        public IEnumerable<VirtualMachineNetworkAdapter> NetworkAdapters { get; set; }

        public IEnumerable<VirtualMachineDrive> Drives { get; set; }
    }
}