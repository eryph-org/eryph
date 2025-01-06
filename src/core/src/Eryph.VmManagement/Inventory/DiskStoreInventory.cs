using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                .SelectMany(e => e.Datastores.ToSeq().Map(ds => ds.Path))
                .ToSeq(),
            vmHostAgentConfig.Environments.ToSeq()
                .Map(e => e.Defaults.Volumes),
            vmHostAgentConfig.Datastores.ToSeq().Map(ds => ds.Path),
            Seq1(vmHostAgentConfig.Defaults.Volumes))
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
        from diskSettings in DiskStorageSettings.FromVhdPath(powershellEngine, vmHostAgentConfig, diskPath)
                                 .ToAff(identity)
                                 .Map(Right<Error, DiskStorageSettings>)
                             | @catch(e => SuccessAff(Left<Error, DiskStorageSettings>(
                                 Error.New($"Inventory of virtual disk '{diskPath}' failed", e))))
        select diskSettings.Map(s => s.CreateDiskInfo());
}
