using System;
using System.IO;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Resources.Disks;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.VmManagement.Inventory;

public static class DiskStoreInventory
{
    public static Aff<Seq<Either<Error, DiskInfo>>> InventoryStores(
        IFileSystemService fileSystemService,
        IPowershellEngine powershellEngine,
        VmHostAgentConfiguration vmHostAgentConfig) =>
        from _ in SuccessAff(unit)
        let storePaths = append(
            vmHostAgentConfig.Environments.ToSeq()
                .Bind(e => e.Datastores.ToSeq())
                .Map(ds => ds.Path),
            vmHostAgentConfig.Environments.ToSeq()
                .Map(e => e.Defaults.Volumes ?? ""),
            vmHostAgentConfig.Datastores.ToSeq()
                .Map(ds => ds.Path),
            Seq1(vmHostAgentConfig.Defaults.Volumes ?? ""))
        from diskInfos in storePaths
            .Map(storePath => InventoryStore(fileSystemService, powershellEngine, vmHostAgentConfig, storePath))
            .SequenceSerial()
        select diskInfos.Flatten();

    public static Aff<Seq<Either<Error, DiskInfo>>> InventoryStore(
        IFileSystemService fileSystemService,
        IPowershellEngine powershellEngine,
        VmHostAgentConfiguration vmHostAgentConfig,
        string path) =>
        from vhdFiles in Eff(() => fileSystemService.GetFiles(path, "*.vhdx", SearchOption.AllDirectories))
        from diskInfos in vhdFiles.ToSeq()
            .Map(vhdFile => InventoryDisk(powershellEngine, vmHostAgentConfig, vhdFile))
            .SequenceParallel()
        select diskInfos;

    private static Aff<Either<Error, DiskInfo>> InventoryDisk(
        IPowershellEngine powershellEngine,
        VmHostAgentConfiguration vmHostAgentConfig,
        string diskPath) =>
        // A VHD can be transiently locked while another operation uses it - most commonly a genepool
        // base disk being attached as the parent of a new catlet's differencing disk, or a .vhdx still
        // being written by a concurrent gene extraction. Get-VHD then fails with "the object is in use".
        // Retry a few times (starting immediately) to ride out that short-lived lock instead of dropping
        // the disk from the inventory pass, which would log a spurious error and trigger a needless
        // disk-existence recheck on the controller. A genuinely unreadable disk still fails and is caught.
        from diskSettings in retry(
                                 Schedule.NoDelayOnFirst & Schedule.spaced(TimeSpan.FromSeconds(2)) & Schedule.recurs(3),
                                 DiskStorageSettings.FromVhdPath(powershellEngine, vmHostAgentConfig, diskPath)
                                     .ToAff(identity))
                                 .Map(Right<Error, DiskStorageSettings>)
                             | @catch(e => SuccessAff(Left<Error, DiskStorageSettings>(
                                 Error.New($"Inventory of virtual disk '{diskPath}' failed", e))))
        select diskSettings.Map(s => s.CreateDiskInfo());
}
