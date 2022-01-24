using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data
{
    public interface IVMWithDrivesInfo<out THardDrive> where THardDrive : IDriveInfo
    {
        DvdDriveInfo[] DVDDrives { get; }
        THardDrive[] HardDrives { get; }
    }
}