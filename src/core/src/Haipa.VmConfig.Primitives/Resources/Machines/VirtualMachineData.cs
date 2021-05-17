using System;

namespace Haipa.Primitives.Resources.Machines
{
    public class VirtualMachineData : MachineData
    {
        public Guid VMId { get; set; }

        public Guid MetadataId { get; set; }


        public VmStatus Status { get; set; }

        public VirtualMachineNetworkAdapterData[] NetworkAdapters { get; set; }
        public VirtualMachineDriveData[] Drives { get; set; }


    }
}