using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

namespace Eryph.VmManagement.Storage
{
    public class CurrentHardDiskDriveStorageSettings : HardDiskDriveStorageSettings
    {
        public bool Frozen { get; set; }
        public string AttachedVMId { get; set; }


        public static EitherAsync<Error, Seq<CurrentHardDiskDriveStorageSettings>> Detect(
            IPowershellEngine engine,
            VmHostAgentConfiguration vmHostAgentConfig,
            IEnumerable<TypedPsObject<VirtualMachineDeviceInfo>> hdInfos)
        {
            var r = hdInfos
                .ToSeq().MapToEitherAsync(hdInfo => 
                    Detect(engine, vmHostAgentConfig, 
                    hdInfo.Cast<HardDiskDriveInfo>()).ToEither())
                .ToAsync()
                .Map(x => x.Where(o => o.IsSome)
                    .Map(o => o.ValueUnsafe()));

            return r;
        }

        public static EitherAsync<Error, Option<CurrentHardDiskDriveStorageSettings>> Detect(
            IPowershellEngine engine,
            VmHostAgentConfiguration vmHostAgentConfig,
            HardDiskDriveInfo hdInfo)
        {
            return Prelude.Cond<HardDiskDriveInfo>(h => !string.IsNullOrWhiteSpace(h.Path))(hdInfo).Map(hd =>
                from vhdInfo in VhdQuery.GetVhdInfo(engine, hdInfo.Path).ToAsync().ToError()
                from snapshotInfo in VhdQuery.GetSnapshotAndActualVhd(engine, vhdInfo).ToAsync().ToError()
                let vhdPath = snapshotInfo.ActualVhd.Map(vhd => vhd.Value.Path)
                let snapshotPath = snapshotInfo.SnapshotVhd.Map(vhd => vhd.Value.Path)
                // The actual VHD might not exist (e.g. if it was deleted in the filesystem)
                from diskSettings in vhdPath
                    .Map(p => DiskStorageSettings.FromVhdPath(engine, vmHostAgentConfig, p))
                    .Sequence()
                select
                    new CurrentHardDiskDriveStorageSettings
                    {
                        Type = CatletDriveType.VHD,
                        AttachPath = snapshotPath | vhdPath | hdInfo.Path,
                        Frozen = !diskSettings.Map(d => d.StorageIdentifier.IsSome || d.Gene.IsSome).IfNone(false)
                                 || !diskSettings.Map(d => d.StorageNames.IsValid).IfNone(false)
                                 || snapshotPath.IsSome,
                        AttachedVMId = hdInfo.Id,
                        ControllerNumber = hdInfo.ControllerNumber,
                        ControllerLocation = hdInfo.ControllerLocation,
                        DiskSettings = diskSettings.IfNoneUnsafe((DiskStorageSettings)null)
                    }).Sequence();
        }
    }
}