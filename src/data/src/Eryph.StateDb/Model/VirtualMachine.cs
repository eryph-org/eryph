using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class VirtualMachine : Machine
    {
        public VirtualMachine()
        {
            MachineType = MachineType.VM;
        }

        public Guid VMId { get; set; }

        public Guid MetadataId { get; set; }

        public string Path { get; set; }
        public virtual VMHostMachine Host { get; set; }


        public virtual List<VirtualMachineNetworkAdapter> NetworkAdapters { get; set; }

        public virtual List<VirtualMachineDrive> Drives { get; set; }
    }
}