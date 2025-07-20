using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data.Planned;

public sealed class PlannedHardDiskDriveInfo : DriveInfo
{
    public long Size { get; set; }
}