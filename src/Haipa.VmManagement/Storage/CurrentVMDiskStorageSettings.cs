using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.VmConfig;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Core;
using LanguageExt;

namespace Haipa.VmManagement.Storage
{
    public class CurrentVMDiskStorageSettings : VMDiskStorageSettings
    {
        public bool Frozen { get; set; }
        public string AttachedVMId { get; set; }


        public static async Task<Either<PowershellFailure, Seq<CurrentVMDiskStorageSettings>>> DetectDiskStorageSettings(IPowershellEngine engine, HostSettings hostSettings, IEnumerable<HardDiskDriveInfo> hdInfos)
        {
            var r = await hdInfos.Where(x => !string.IsNullOrWhiteSpace(x.Path))
                .ToSeq().MapToEitherAsync(hdInfo =>
                {
                    var res =
                        (
                        from vhdInfo in VhdQuery.GetVhdInfo(engine, hdInfo.Path).ToAsync()
                        from snapshotInfo in VhdQuery.GetSnapshotAndActualVhd(engine, vhdInfo).ToAsync()
                        let vhdPath = snapshotInfo.ActualVhd.Map(vhd => vhd.Value.Path).IfNone(hdInfo.Path)
                        let parentPath = snapshotInfo.ActualVhd.Map(vhd => vhd.Value.ParentPath)
                        let snapshotPath = snapshotInfo.SnapshotVhd.Map(vhd => vhd.Value.Path)
                        let nameAndId = StorageNames.FromPath(System.IO.Path.GetDirectoryName(vhdPath), hostSettings.DefaultVirtualHardDiskPath)
                        let diskSize = vhdInfo.Map(v => v.Value.Size).IfNone(0)
                        select
                            new CurrentVMDiskStorageSettings
                            {
                                Type = VirtualMachineDriveType.VHD,
                                Path = System.IO.Path.GetDirectoryName(vhdPath),
                                Name = System.IO.Path.GetFileNameWithoutExtension(vhdPath),
                                AttachPath = snapshotPath.IsSome ? snapshotPath : vhdPath,
                                ParentPath = parentPath,
                                StorageNames = nameAndId.Names,
                                StorageIdentifier = nameAndId.StorageIdentifier,
                                SizeBytes = diskSize,
                                Frozen = snapshotPath.IsSome,
                                AttachedVMId = hdInfo.Id,
                                ControllerNumber = hdInfo.ControllerNumber,
                                ControllerLocation = hdInfo.ControllerLocation,
                            }).ToEither();
                    return res;
                });

            return r;
        }
    }
}