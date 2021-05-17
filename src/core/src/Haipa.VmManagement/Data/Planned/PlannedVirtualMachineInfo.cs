using System;
using Haipa.VmManagement.Data.Core;
using Haipa.VmManagement.Data.Full;
using LanguageExt;

namespace Haipa.VmManagement.Data.Planned
{
    public sealed class PlannedVirtualMachineInfo : Record<VirtualMachineInfo>,
        IVirtualMachineCoreInfo, IVMWithNetworkAdapterInfo<PlannedVMNetworkAdapter>,
        IVMWithDrivesInfo<PlannedHardDiskDriveInfo>

    {
        public long MemoryMaximum { get; private set; }

        public long MemoryMinimum { get; private set; }
        public Guid Id { get; private set; }

        public string Name { get; private set; }

        public long ProcessorCount { get; private set; }
        public int Generation { get; private set; }


        public string Path { get; private set; }
        public long MemoryStartup { get; private set; }


        public DvdDriveInfo[] DVDDrives { get; private set; }
        public PlannedHardDiskDriveInfo[] HardDrives { get; private set; }

        public PlannedVMNetworkAdapter[] NetworkAdapters { get; private set; }
    }

    public sealed class PlannedHardDiskDriveInfo : DriveInfo, IDriveInfo
    {
        public long Size { get; set; }
    }
}