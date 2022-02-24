using System;
using Eryph.Core;

namespace Eryph.Resources.Machines
{
    public class VirtualMachineData : MachineData
    {
        [PrivateIdentifier]
        public Guid VMId { get; set; }

        public Guid MetadataId { get; set; }


        public VmStatus Status { get; set; }

        public VirtualMachineNetworkAdapterData[] NetworkAdapters { get; set; }
        public VirtualMachineDriveData[] Drives { get; set; }
    }
}