using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Resources.Machines;
using Haipa.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace Haipa.VmManagement.Storage
{
    public class CurrentHardDiskDriveStorageSettings : HardDiskDriveStorageSettings
    {
        public bool Frozen { get; set; }
        public string AttachedVMId { get; set; }


        public static Task<Either<PowershellFailure, Seq<CurrentHardDiskDriveStorageSettings>>> Detect(
            IPowershellEngine engine, HostSettings hostSettings, IEnumerable<HardDiskDriveInfo> hdInfos)
        {
            var r = hdInfos
                .ToSeq().MapToEitherAsync(hdInfo => Detect(engine, hostSettings, hdInfo))
                .MapAsync(x => x.Where(o => o.IsSome)
                    .Map(o => o.ValueUnsafe()));

            return r;
        }

        public static Task<Either<PowershellFailure, Option<CurrentHardDiskDriveStorageSettings>>> Detect(
            IPowershellEngine engine, HostSettings hostSettings, HardDiskDriveInfo hdInfo)
        {
            return Prelude.Cond<HardDiskDriveInfo>(h => !string.IsNullOrWhiteSpace(h.Path))(hdInfo).Map(hd =>
                from vhdInfo in VhdQuery.GetVhdInfo(engine, hdInfo.Path).ToAsync()
                from snapshotInfo in VhdQuery.GetSnapshotAndActualVhd(engine, vhdInfo).ToAsync()
                let vhdPath = snapshotInfo.ActualVhd.Map(vhd => vhd.Value.Path).IfNone(hdInfo.Path)
                let snapshotPath = snapshotInfo.SnapshotVhd.Map(vhd => vhd.Value.Path)
                from optionalDiskSettings in DiskStorageSettings.FromVhdPath(engine, hostSettings, vhdPath).ToAsync()
                from diskSettings in optionalDiskSettings.ToEither(new PowershellFailure
                    {Message = "Missing disk settings for existing disk. Should not happen."}).ToAsync()
                select
                    new CurrentHardDiskDriveStorageSettings
                    {
                        Type = VirtualMachineDriveType.VHD,
                        AttachPath = snapshotPath.IsSome ? snapshotPath : vhdPath,
                        Frozen = snapshotPath.IsSome,
                        AttachedVMId = hdInfo.Id,
                        ControllerNumber = hdInfo.ControllerNumber,
                        ControllerLocation = hdInfo.ControllerLocation,
                        DiskSettings = diskSettings
                    }).Traverse(l => l).ToEither();
        }
    }
}