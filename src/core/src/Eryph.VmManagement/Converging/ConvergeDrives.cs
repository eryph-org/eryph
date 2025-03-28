using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeDrives : ConvergeTaskBase
    {
        public ConvergeDrives(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<Error, Unit>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            if (Context.StorageSettings.Frozen)
                return Right<Error, Unit>(unit);

            var currentCheckpointType = vmInfo.Value.CheckpointType;

            try
            {
                 var res = await (
                    //prevent snapshots creating during running disk converge
                    from _ in SetVMCheckpointType(vmInfo, CheckpointType.Disabled, Context.Engine)
                    //make a plan
                    from plannedDriveStorageSettings in VMDriveStorageSettings.PlanDriveStorageSettings(
                        Context.VmHostAgentConfig, Context.Config, Context.StorageSettings,
                        path => VhdQuery.GetVhdInfo(Context.Engine, path).MapT(o => o.Value),
                        path => VhdQuery.TestVhd(Context.Engine, path),
                        Context.ResolvedGenes)
                    //ensure that the changes reflect the current VM settings
                    from infoReloaded in vmInfo.Reload(Context.Engine)
                    //detach removed disks
                    from __ in DetachUndefinedDrives(infoReloaded, plannedDriveStorageSettings)
                    from infoRecreated in vmInfo.Reload(Context.Engine)
                    from ___ in VirtualDisks(infoRecreated, plannedDriveStorageSettings)
                    from ____ in DvdDrives(infoRecreated, plannedDriveStorageSettings)
                    select Unit.Default).ToEither().ConfigureAwait(false);

                 return res;
            }
            finally
            {
                await SetVMCheckpointType(vmInfo, currentCheckpointType, Context.Engine);
            }
        }

        private EitherAsync<Error, Unit> VirtualDisks(TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedDriveStorageSettings)
        {
            var plannedDiskSettings = plannedDriveStorageSettings
                .Where(x => x.Type is CatletDriveType.VHD or CatletDriveType.SharedVHD or CatletDriveType.VHDSet)
                .Cast<HardDiskDriveStorageSettings>().ToSeq();

            return (from currentDiskSettings in CurrentHardDiskDriveStorageSettings.Detect(Context.Engine,
                    Context.VmHostAgentConfig, vmInfo.GetList(x=>x.HardDrives))
                from _ in plannedDiskSettings.MapToEitherAsync(s => ConvergeVirtualDisk(s, vmInfo, currentDiskSettings).ToEither()).ToAsync()
                select Unit.Default);
        }
        
        private EitherAsync<Error, Unit> DvdDrives(TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedDriveStorageSettings)
        {
            var plannedDvdSettings = plannedDriveStorageSettings
                .Where(x => x.Type == CatletDriveType.DVD)
                .Cast<VMDvDStorageSettings>().ToSeq();

            return (
                from _ in plannedDvdSettings.MapToEitherAsync(s => DvdDrive(s, vmInfo).ToEither())
                    .ToAsync()
                select Unit.Default);
        }


        private static EitherAsync<Error, Unit> SetVMCheckpointType(
            TypedPsObject<VirtualMachineInfo> vmInfo, CheckpointType checkpointType, IPowershellEngine engine)
        {
            return engine.RunAsync(new PsCommandBuilder()
                .AddCommand("Set-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("CheckpointType", checkpointType));
        }

        private EitherAsync<Error, Unit> DetachUndefinedDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo, Seq<VMDriveStorageSettings> plannedStorageSettings)

        {
            return from currentDiskSettings in CurrentHardDiskDriveStorageSettings
                    .Detect(Context.Engine, Context.VmHostAgentConfig, vmInfo.GetList(x => x.HardDrives))
                from _ in DetachUndefinedHardDrives(vmInfo, plannedStorageSettings, currentDiskSettings)
                from __ in DetachUndefinedDvdDrives(vmInfo, plannedStorageSettings)
                select Unit.Default;
        }


        private EitherAsync<Error, Unit> DetachUndefinedHardDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedStorageSettings,
            Seq<CurrentHardDiskDriveStorageSettings> currentDiskStorageSettings) =>
            from _1 in RightAsync<Error, Unit>(unit)
            let plannedDiskSettings = plannedStorageSettings.Where(x =>
                    x.Type is CatletDriveType.VHD or CatletDriveType.SharedVHD or CatletDriveType.VHDSet)
                .Cast<HardDiskDriveStorageSettings>().ToSeq()
            let frozenDiskIds = currentDiskStorageSettings.Where(x => x.Frozen).Map(x => x.AttachedVMId)
            from _2 in ConvergeHelpers.FindAndApply(vmInfo,
                i => i.HardDrives,
                device =>
                {
                    var hd = device.Cast<HardDiskDriveInfo>().Value;

                    var plannedDiskAtControllerPos = plannedDiskSettings
                        .FirstOrDefault(x =>
                            x.ControllerLocation == hd.ControllerLocation &&
                            x.ControllerNumber == hd.ControllerNumber);

                    var detach = plannedDiskAtControllerPos == null;

                    if (!detach && plannedDiskAtControllerPos.AttachPath.IsSome)
                    {
                        var plannedAttachPath = plannedDiskAtControllerPos.AttachPath.IfNone("");
                        if (hd.Path == null || !hd.Path.Equals(plannedAttachPath,
                                StringComparison.OrdinalIgnoreCase))
                            detach = true;
                    }

                    if (detach && frozenDiskIds.Contains(hd.Id))
                    {
                        Context.ReportProgress(hd.Path != null
                            ? $"Skipping detach of frozen disk {Path.GetFileNameWithoutExtension(hd.Path)}"
                            : $"Skipping detach of unknown frozen disk at controller {hd.ControllerNumber}, Location: {hd.ControllerLocation}");

                        return false;
                    }

                    if (detach)
                        Context.ReportProgress(hd.Path != null
                            ? $"Detaching disk {Path.GetFileNameWithoutExtension(hd.Path)} from controller: {hd.ControllerNumber}, Location: {hd.ControllerLocation}"
                            : $"Detaching unknown disk at controller: {hd.ControllerNumber}, Location: {hd.ControllerLocation}");

                    return detach;
                },
                d => from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Remove-VMHardDiskDrive")
                        .AddParameter("VMHardDiskDrive", d.PsObject))
                    select d)
            select unit;

        private EitherAsync<Error, Unit> DetachUndefinedDvdDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedStorageSettings) =>
            from _1 in RightAsync<Error, Unit>(unit)
            let plannedDvdDrives = plannedStorageSettings
                .Filter(c => c.Type is CatletDriveType.DVD)
                .Map(c => (Number: c.ControllerNumber, Location: c.ControllerLocation))
            from _2 in ConvergeHelpers.FindAndApply(
                vmInfo,
                i => i.DVDDrives,
                device =>
                {
                    var dvd = device.Cast<DvdDriveInfo>().Value;
                    // Ignore cloud init drive, will be handled later
                    return !(dvd.ControllerNumber == 0 && dvd.ControllerLocation == 63)
                           && !plannedDvdDrives.Contains((dvd.ControllerNumber, dvd.ControllerLocation));
                },
                d => from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Remove-VMDvdDrive")
                        .AddParameter("VMDvdDrive", d.PsObject))
                     select d)
            select unit;

        private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> DvdDrive(
            VMDvDStorageSettings dvdSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo) =>
            from dvdDrive in ConvergeHelpers.GetOrCreateInfoAsync(
                vmInfo,
                l => l.DVDDrives,
                device => device.Cast<DvdDriveInfo>()
                    .Map(drive => drive.ControllerLocation == dvdSettings.ControllerLocation
                                  && drive.ControllerNumber == dvdSettings.ControllerNumber),
                () => from _ in Context.ReportProgressAsync(
                              $"Attaching DVD Drive to controller: {dvdSettings.ControllerNumber}, Location: {dvdSettings.ControllerLocation}")
                      from created in Context.Engine.GetObjectAsync<VirtualMachineDeviceInfo>(
                          PsCommandBuilder.Create()
                              .AddCommand("Add-VMDvdDrive")
                              .AddParameter("VM", vmInfo.PsObject)
                              .AddParameter("ControllerNumber", dvdSettings.ControllerNumber)
                              .AddParameter("ControllerLocation", dvdSettings.ControllerLocation)
                              .AddParameter("PassThru"))
                      select created)
            from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMDvdDrive")
                .AddParameter("VMDvdDrive", dvdDrive.PsObject)
                .AddParameter("Path", dvdSettings.Path))
            from vmInfoRecreated in vmInfo.Reload(Context.Engine)
            select vmInfoRecreated;

        private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ConvergeVirtualDisk(
            HardDiskDriveStorageSettings driveSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<CurrentHardDiskDriveStorageSettings> currentStorageSettings) =>
            from vhdPath in driveSettings.AttachPath.ToEitherAsync(Error.New(
                $"The path is missing for virtual disk {driveSettings.ControllerNumber},{driveSettings.ControllerLocation}"))
            let currentSettings = currentStorageSettings.Find(x =>
                // The attach path might point to a snapshot, so we need to compare the actual VHD path.
                string.Equals(vhdPath, Path.Combine(x.DiskSettings.Path, x.DiskSettings.FileName), StringComparison.OrdinalIgnoreCase))
            let isFrozen = currentSettings.Map(s => s.Frozen).IfNone(false)
            from reloadedVmInfo in isFrozen switch
            {
                true =>
                    from _ in Context.ReportProgressAsync(
                        $"Skipping HD Drive '{driveSettings.DiskSettings.Name}': storage management is disabled for this disk.")
                    select vmInfo,
                false =>
                    from testPathResult in Context.Engine.GetObjectValueAsync<bool>(
                            new PsCommandBuilder().AddCommand("Test-Path").AddArgument(vhdPath))
                    let fileExists = testPathResult.IfNone(false)
                    from __ in fileExists
                        ? UpdateVirtualDisk(driveSettings)
                        : CreateVirtualDisk(driveSettings)
                    from uAttach in ConvergeHelpers.GetOrCreateInfoAsync(
                        vmInfo,
                        i => i.HardDrives,
                        device => device.Cast<HardDiskDriveInfo>()
                            .Map(disk => currentSettings.Map(x => x.AttachedVMId) == disk.Id),
                        () => from _  in Context.ReportProgressAsync(
                                  $"Attaching HD Drive {driveSettings.DiskSettings.Name} to controller: {driveSettings.ControllerNumber}, Location: {driveSettings.ControllerLocation}")
                              from result in Context.Engine.GetObjectAsync<VirtualMachineDeviceInfo>(
                                  BuildAttachCommand(vmInfo, vhdPath, driveSettings))
                              select result)
                    from reloadedVmInfo in vmInfo.Reload(Context.Engine)
                    select reloadedVmInfo
            }
            select reloadedVmInfo;


        private EitherAsync<Error, Unit> CreateVirtualDisk(
            HardDiskDriveStorageSettings driveSettings) =>
            from vhdPath in driveSettings.AttachPath.ToEitherAsync(Error.New(
                $"The path is missing for virtual disk {driveSettings.ControllerNumber},{driveSettings.ControllerLocation}"))
            from p1 in Context.ReportProgressAsync($"Creating HD Drive: {driveSettings.DiskSettings.Name}")
            from uCreate in driveSettings.Type switch
            {
                CatletDriveType.SharedVHD or CatletDriveType.VHDSet =>
                    driveSettings.DiskSettings.ParentSettings.Match(
                        Some: parentSettings =>
                            from _ in RightAsync<Error, Unit>(unit)
                            // Shared VHDs and VHD sets don't support differencing disks.
                            // Hence, we need to copy the parent disk (and then convert it to .vhds in case of a VHD set)
                            let parentFilePath = Path.Combine(parentSettings.Path, parentSettings.FileName)
                            // For shared VHD this doesn't change the path, but for a VHD set it does.
                            let copyToPath = Path.ChangeExtension(vhdPath, "vhdx")
                            from __ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                                .AddCommand("Copy-Item")
                                .AddArgument(parentFilePath)
                                .AddArgument(copyToPath)
                                .AddParameter("Force"))
                            from ___ in ResetDiskIdentifier(copyToPath)
                            from ____ in driveSettings.Type == CatletDriveType.SharedVHD
                                ? RightAsync<Error, Unit>(unit)
                                : Context.Engine.RunAsync(PsCommandBuilder.Create()
                                    .AddCommand("Convert-VHD")
                                    .AddArgument(copyToPath)
                                    .AddArgument(vhdPath))
                            select unit,
                        None: () =>
                            from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                                .AddCommand("New-VHD")
                                .AddParameter("Path", vhdPath)
                                .AddParameter("Dynamic")
                                .AddParameter("SizeBytes", driveSettings.DiskSettings.SizeBytesCreate))
                            select unit),
                _ => driveSettings.DiskSettings.ParentSettings.Match(
                    Some: parentSettings =>
                        from _ in RightAsync<Error, Unit>(unit)
                        let parentFilePath = Path.Combine(parentSettings.Path, parentSettings.FileName)
                        let newCommand = PsCommandBuilder.Create()
                            .AddCommand("New-VHD")
                            .AddParameter("Path", vhdPath)
                            .AddParameter("ParentPath", parentFilePath)
                            .AddParameter("Differencing")
                            .AddParameter("SizeBytes", driveSettings.DiskSettings.SizeBytesCreate)
                        from __ in Context.Engine.RunAsync(newCommand)
                        from ___ in ResetDiskIdentifier(vhdPath)
                        select unit,
                    None: () =>
                        from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("New-VHD")
                            .AddParameter("Path", vhdPath)
                            .AddParameter("Dynamic")
                            .AddParameter("SizeBytes", driveSettings.DiskSettings.SizeBytesCreate))
                        select unit)
            }
            select unit;

        private EitherAsync<Error, Unit> ResetDiskIdentifier(string path) =>
            from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Set-VHD")
                    .AddParameter("Path", path)
                    .AddParameter("ResetDiskIdentifier")
                    .AddParameter("Force"))
            select unit;

        private EitherAsync<Error, Unit> UpdateVirtualDisk(
            HardDiskDriveStorageSettings driveSettings) =>
            from vhdPath in driveSettings.AttachPath.ToEitherAsync(Error.New(
                $"The path is missing for virtual disk {driveSettings.ControllerNumber},{driveSettings.ControllerLocation}"))
            // get disk
            from optionalVhdInfo in Context.Engine.GetObjectAsync<VhdInfo>(new PsCommandBuilder()
                    .AddCommand("Get-VHD").AddArgument(vhdPath))
            from vhdInfo in optionalVhdInfo.ToEitherAsync(Error.New(
                $"Could not find the virtual disk '{vhdPath}'."))
            // resize if necessary
            let newSize = driveSettings.DiskSettings.SizeBytes.GetValueOrDefault()
            let hasCorrectSize = newSize == 0 || vhdInfo.Value.Size == newSize
            from _ in hasCorrectSize
                ? RightAsync<Error, Unit>(unit)
                : from _ in RightAsync<Error, Unit>(unit)
                  let diskSizeUpdateInGb = Math.Round(newSize / 1024d / 1024 / 1024, 1)
                  from pS in Context.ReportProgressAsync(
                      $"Resizing disk {driveSettings.DiskSettings.Name} to {diskSizeUpdateInGb} GB")
                  from uResize in Context.Engine.RunAsync(PsCommandBuilder.Create()
                          .AddCommand("Resize-VHD")
                          .AddParameter("Path", vhdPath)
                          .AddParameter("SizeBytes", driveSettings.DiskSettings.SizeBytes))
                  select unit
            select unit;

        private static PsCommandBuilder BuildAttachCommand(TypedPsObject<VirtualMachineInfo> vmInfo, string vhdPath, VMDriveStorageSettings driveSettings)
        {
            var command = PsCommandBuilder.Create()
                .AddCommand("Add-VMHardDiskDrive")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Path", vhdPath);

            if (driveSettings.Type == CatletDriveType.SharedVHD)
                command.AddParameter("SupportPersistentReservations");

            command.AddParameter("ControllerNumber", driveSettings.ControllerNumber)
                .AddParameter("ControllerLocation", driveSettings.ControllerLocation)
                .AddParameter("PassThru");

            return command;
        }
    }
}