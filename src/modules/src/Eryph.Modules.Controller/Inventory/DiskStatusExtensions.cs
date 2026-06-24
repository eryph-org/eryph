using System;
using Eryph.Resources.Disks;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Inventory;

internal static class DiskStatusExtensions
{
    public static VirtualDiskStatus ToVirtualDiskStatus(this DiskStatus diskStatus) =>
        diskStatus switch
        {
            DiskStatus.Ok => VirtualDiskStatus.Ok,
            DiskStatus.Error => VirtualDiskStatus.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(diskStatus), diskStatus,
                $"The status {diskStatus} is not supported"),
        };
}
