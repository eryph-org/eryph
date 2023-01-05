using System;
using Eryph.ConfigModel;

namespace Eryph.Resources.Machines
{
    public class VirtualMachineData : MachineData
    {
        public Guid VMId { get; set; }

        public Guid MetadataId { get; set; }

        public VmStatus Status { get; set; }
        public TimeSpan UpTime { get; set; }

        public VirtualMachineNetworkAdapterData[] NetworkAdapters { get; set; }
        public VirtualMachineDriveData[] Drives { get; set; }

        public VirtualMachineCpuData Cpu { get; set; }
        public VirtualMachineMemoryData Memory { get; set; }

        public VirtualMachineFirmwareData Firmware { get; set; }

    }
}