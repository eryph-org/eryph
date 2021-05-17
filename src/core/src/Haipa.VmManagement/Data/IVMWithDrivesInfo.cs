using Haipa.VmManagement.Data.Core;

namespace Haipa.VmManagement.Data
{
    public interface IVMWithDrivesInfo<out THardDrive> where THardDrive : IDriveInfo
    {
        DvdDriveInfo[] DVDDrives { get; }
        THardDrive[] HardDrives { get; }
    }
}