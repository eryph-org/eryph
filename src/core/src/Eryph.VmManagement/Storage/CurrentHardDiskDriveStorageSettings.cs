using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage;

public class CurrentHardDiskDriveStorageSettings : HardDiskDriveStorageSettings
{
    public bool Frozen { get; set; }

    public string AttachedVMId { get; set; }

    public static EitherAsync<Error, Seq<CurrentHardDiskDriveStorageSettings>> Detect(
        IPowershellEngine engine,
        VmHostAgentConfiguration vmHostAgentConfig,
        Seq<TypedPsObject<VirtualMachineDeviceInfo>> deviceInfos) =>
        from hdInfos in deviceInfos
            .Map(deviceInfo => deviceInfo.CastSafe<HardDiskDriveInfo>().ToError())
            .Sequence()
            .ToAsync()
        from result in hdInfos
            .Filter(hdInfo => notEmpty(hdInfo.Value.Path))
            .Map(hdInfo => Detect(engine, vmHostAgentConfig, hdInfo))
            .SequenceSerial()
        select result;

    public static EitherAsync<Error, CurrentHardDiskDriveStorageSettings> Detect(
        IPowershellEngine engine,
        VmHostAgentConfiguration vmHostAgentConfig,
        HardDiskDriveInfo hdInfo) =>
        from _ in guard(notEmpty(hdInfo.Path),
                Error.New("BUG! The hard disk drive info has no path."))
            .ToEitherAsync()
        from vhdInfo in VhdQuery.GetVhdInfo(engine, hdInfo.Path)
        from snapshotInfo in vhdInfo
            .Map(i => VhdQuery.GetSnapshotAndActualVhd(engine, i))
            .Sequence()
        let vhdPath = snapshotInfo.Map(si => si.ActualVhd.Value.Path)
        let snapshotPath = snapshotInfo.Bind(si => si.SnapshotVhd).Map(vhd => vhd.Value.Path)
        // The actual VHD might not exist (e.g. if it was deleted in the filesystem)
        from diskSettings in vhdPath
            .Map(p => DiskStorageSettings.FromVhdPath(engine, vmHostAgentConfig, p))
            .Sequence()
        select new CurrentHardDiskDriveStorageSettings
        {
            Type = CatletDriveType.VHD,
            AttachPath = snapshotPath | vhdPath | hdInfo.Path,
            Frozen = !diskSettings.Map(d => d.StorageIdentifier.IsSome || d.Gene.IsSome).IfNone(false)
                     || !diskSettings.Map(d => d.StorageNames.IsValid).IfNone(false)
                     || !diskSettings.Map(d => d.IsValid).IfNone(false)
                     || snapshotPath.IsSome,
            AttachedVMId = hdInfo.Id,
            ControllerNumber = hdInfo.ControllerNumber,
            ControllerLocation = hdInfo.ControllerLocation,
            DiskSettings = diskSettings.IfNoneUnsafe((DiskStorageSettings)null)
        };
}
