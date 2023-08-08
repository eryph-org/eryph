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

        public string DataStore { get; set; }
        public string ProjectName { get; set; }
        public string Environment { get; set; }

        public bool Frozen { get; set; }
        public string VMPath { get; set; }
        public string StorageIdentifier { get; set; }

        public VirtualMachineNetworkAdapterData[] NetworkAdapters { get; set; }
        public VirtualMachineDriveData[] Drives { get; set; }

        public VirtualMachineCpuData Cpu { get; set; }
        public VirtualMachineMemoryData Memory { get; set; }

        public VirtualMachineFirmwareData Firmware { get; set; }

    }
}