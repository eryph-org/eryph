using System;
using System.Collections.Generic;

namespace Haipa.StateDb.Model
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
        public VMHostMachine Host { get; set; }


        public virtual List<VirtualMachineNetworkAdapter> NetworkAdapters { get; set; }

        public virtual List<VirtualMachineDrive> Drives { get; set; }
    }
}