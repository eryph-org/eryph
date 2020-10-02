using System;

namespace Haipa.VmManagement.Data
{
    public interface IVirtualMachineCoreInfo
    {
        Guid Id { get; }
        string Name { get; }
        DvdDriveInfo[] DVDDrives { get; }
        HardDiskDriveInfo[] HardDrives { get; }
        int Generation { get; }
        bool IsClustered { get; }
        string Path { get; }
    }
}