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
    /// <summary>
    /// Error code marking the inventory of a virtual disk that exists on disk but could not be read
    /// because it is currently in use (transiently locked). Callers treat this as a benign, expected
    /// condition rather than an inventory failure - see <see cref="ClassifyInventoryError"/>.
    /// </summary>
    public const int DiskInUseErrorCode = 30001;

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
            .Map(vhdFile => InventoryDisk(fileSystemService, powershellEngine, vmHostAgentConfig, vhdFile))
            .SequenceParallel()
        select diskInfos;

    private static Aff<Either<Error, DiskInfo>> InventoryDisk(
        IFileSystemService fileSystemService,
        IPowershellEngine powershellEngine,
        VmHostAgentConfiguration vmHostAgentConfig,
        string diskPath) =>
        from diskSettings in DiskStorageSettings.FromVhdPath(powershellEngine, vmHostAgentConfig, diskPath)
                                 .ToAff(identity)
                                 .Map(Right<Error, DiskStorageSettings>)
                             | @catch(e => SuccessAff(Left<Error, DiskStorageSettings>(
                                 ClassifyInventoryError(fileSystemService, diskPath, e))))
        select diskSettings.Map(s => s.CreateDiskInfo());

    // A VHD that cannot be read but still exists on disk is almost always transiently locked by a
    // concurrent operation - most commonly a genepool base disk being attached as the parent of a new
    // catlet's differencing disk, so Get-VHD fails with "the object is in use". That lock can outlast any
    // reasonable retry and is a benign, expected condition, not an inventory failure: the disk is picked
    // up again on a later pass once the lock clears. Mark it with a dedicated code so callers log it
    // quietly. A disk whose file is genuinely gone (or otherwise broken) keeps the normal failure error.
    private static Error ClassifyInventoryError(
        IFileSystemService fileSystemService,
        string diskPath,
        Error error) =>
        fileSystemService.FileExists(diskPath)
            ? Error.New(DiskInUseErrorCode, $"Virtual disk '{diskPath}' is currently in use and was skipped.", error)
            : Error.New($"Inventory of virtual disk '{diskPath}' failed", error);
}
