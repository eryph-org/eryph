using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeDrives : ConvergeTaskBase
    {
        public ConvergeDrives(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            if (Context.StorageSettings.Frozen)
                return Prelude.Right<Error, TypedPsObject<VirtualMachineInfo>>(vmInfo);

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
                    from infoRecreated in vmInfo.RecreateOrReload(Context.Engine)
                    from ___ in VirtualDisks(infoRecreated, plannedDriveStorageSettings)
                    from ____ in DvdDrives(infoRecreated, plannedDriveStorageSettings)
                    select Unit.Default).ToEither().ConfigureAwait(false);

                 if (res.IsLeft)
                     return res.Map(_ => vmInfo);
            }
            finally
            {
                await SetVMCheckpointType(vmInfo, currentCheckpointType, Context.Engine).ToEither().ConfigureAwait(false);
            }

            return await vmInfo.Reload(Context.Engine).ToEither().ConfigureAwait(false);
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
                .AddParameter("CheckpointType", checkpointType)).ToAsync().ToError();
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
            TypedPsObject<VirtualMachineInfo> vmInfo, Seq<VMDriveStorageSettings> plannedStorageSettings,
            Seq<CurrentHardDiskDriveStorageSettings> currentDiskStorageSettings)

        {
            var planedDiskSettings = plannedStorageSettings.Where(x =>
                    x.Type is CatletDriveType.VHD or CatletDriveType.SharedVHD or CatletDriveType.VHDSet)
                .Cast<HardDiskDriveStorageSettings>().ToSeq();

            var frozenDiskIds = currentDiskStorageSettings.Where(x => x.Frozen).Map(x => x.AttachedVMId);

            return ConvergeHelpers.FindAndApply(vmInfo,
                    i => i.HardDrives,
                    device =>
                    {
                        var hd = device.Cast<HardDiskDriveInfo>().Value;

                        var plannedDiskAtControllerPos = planedDiskSettings
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
                    i => Context.Engine.RunAsync(PsCommandBuilder.Create().AddCommand("Remove-VMHardDiskDrive")
                        .AddParameter("VMHardDiskDrive", i.PsObject))).Map(x => x.Lefts().HeadOrNone())
                        .MatchAsync(
                            l => Prelude.LeftAsync<PowershellFailure, Unit>(l).ToEither(),
                            () => Prelude.RightAsync<PowershellFailure, Unit>(Unit.Default)
                                .ToEither()).ToError().ToAsync();
        }


        private EitherAsync<Error, Unit> DetachUndefinedDvdDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo, Seq<VMDriveStorageSettings> plannedStorageSettings)

        {
            var controllersAndLocations = plannedStorageSettings.Where(x => x.Type == CatletDriveType.DVD)
                .Map(x => new {x.ControllerNumber, x.ControllerLocation})
                .GroupBy(x => x.ControllerNumber)
                .ToImmutableDictionary(x => x.Key, x => x.Map(y => y.ControllerLocation).ToImmutableArray());


            return ConvergeHelpers.FindAndApply(vmInfo,
                    i => i.DVDDrives,
                    device =>
                    {
                        var dvd = device.Cast<DvdDriveInfo>().Value;
                        //ignore cloud init drive, will be handled later
                        if (dvd.ControllerLocation == 63 && dvd.ControllerNumber == 0)
                            return false;

                        var detach = !controllersAndLocations.ContainsKey(dvd.ControllerNumber) ||
                                     !controllersAndLocations[dvd.ControllerNumber].Contains(dvd.ControllerLocation);

                        return detach;
                    },
                    i => Context.Engine.RunAsync(PsCommandBuilder.Create().AddCommand("Remove-VMDvdDrive")
                        .AddParameter("VMDvdDrive", i.PsObject))).Map(x => x.Lefts().HeadOrNone())
                .MatchAsync(
                    l => Prelude.LeftAsync<PowershellFailure, Unit>(l).ToEither(),
                    () => Prelude.RightAsync<PowershellFailure, Unit>(Unit.Default)
                        .ToEither()).ToError().ToAsync();
        }

        private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> DvdDrive(
            VMDvDStorageSettings dvdSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            return
                from dvdDrive in ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
                        l => l.DVDDrives,
                        device => device.Cast<DvdDriveInfo>()
                            .Map(drive => drive.ControllerLocation == dvdSettings.ControllerLocation
                                          && drive.ControllerNumber == dvdSettings.ControllerNumber),
                        async () =>
                        {
                            await Context
                                .ReportProgress(
                                    $"Attaching DVD Drive to controller: {dvdSettings.ControllerNumber}, Location: {dvdSettings.ControllerLocation}")
                                .ConfigureAwait(false);

                            return await Context.Engine.GetObjectsAsync<VirtualMachineDeviceInfo>(
                                PsCommandBuilder.Create().AddCommand("Add-VMDvdDrive")
                                    .AddParameter("VM", vmInfo.PsObject)
                                    .AddParameter("ControllerNumber", dvdSettings.ControllerNumber)
                                    .AddParameter("ControllerLocation", dvdSettings.ControllerLocation)
                                    .AddParameter("PassThru"));
                        }).ToAsync()
                    from _ in Context.Engine.Run(PsCommandBuilder.Create()
                        .AddCommand("Set-VMDvdDrive")
                        .AddParameter("VMDvdDrive", dvdDrive.PsObject)
                        .AddParameter("Path", dvdSettings.Path)).ToAsync().ToError()
                    from vmInfoRecreated in vmInfo.RecreateOrReload(Context.Engine)
                    select vmInfoRecreated;

        }

        private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ConvergeVirtualDisk(
            HardDiskDriveStorageSettings driveSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<CurrentHardDiskDriveStorageSettings> currentStorageSettings) =>
            from vhdPath in driveSettings.AttachPath.ToEitherAsync(Error.New(
                $"The path is missing for virtual disk {driveSettings.ControllerNumber},{driveSettings.ControllerLocation}"))
            let currentSettings = currentStorageSettings.Find(x =>
                string.Equals(vhdPath, x.AttachPath.IfNone(""), StringComparison.OrdinalIgnoreCase))
            let isFrozen = currentSettings.Map(s => s.Frozen).IfNone(false)
            from reloadedVmInfo in isFrozen switch
            {
                true =>
                    from _ in Context.ReportProgressAsync(
                        $"Skipping HD Drive '{driveSettings.DiskSettings.Name}': storage management is disabled for this disk.")
                    select vmInfo,
                false =>
                    from testPathResult in Context.Engine.GetObjectsAsync<bool>(
                            new PsCommandBuilder().AddCommand("Test-Path").AddArgument(vhdPath))
                        .ToError().ToAsync()
                    let fileExists = testPathResult.Any(v => v.Value)
                    from __ in fileExists
                        ? UpdateVirtualDisk(driveSettings)
                        : CreateVirtualDisk(driveSettings)
                    from uAttach in ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
                        i => i.HardDrives,
                        device => device.Cast<HardDiskDriveInfo>()
                            .Map(disk => currentSettings.Map(x => x.AttachedVMId) == disk.Id),
                        async () =>
                        {
                            await Context
                                .ReportProgress(
                                    $"Attaching HD Drive {driveSettings.DiskSettings.Name} to controller: {driveSettings.ControllerNumber}, Location: {driveSettings.ControllerLocation}")
                                .ConfigureAwait(false);
                            return await Context.Engine.GetObjectsAsync<VirtualMachineDeviceInfo>(
                                BuildAttachCommand(vmInfo, vhdPath, driveSettings)
                            ).ConfigureAwait(false);
                        }).ToAsync()
                    from reloadedVmInfo in vmInfo.RecreateOrReload(Context.Engine)
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
                                .AddParameter("Force")).ToAsync().ToError()
                            from ___ in ResetDiskIdentifier(copyToPath)
                            from ____ in driveSettings.Type == CatletDriveType.SharedVHD
                                ? RightAsync<Error, Unit>(unit)
                                : Context.Engine.RunAsync(PsCommandBuilder.Create()
                                    .AddCommand("Convert-VHD")
                                    .AddArgument(copyToPath)
                                    .AddArgument(vhdPath)).ToError().ToAsync()
                            select unit,
                        None: () =>
                            from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                                .AddCommand("New-VHD")
                                .AddParameter("Path", vhdPath)
                                .AddParameter("Dynamic")
                                .AddParameter("SizeBytes", driveSettings.DiskSettings.SizeBytesCreate))
                                .ToError().ToAsync()
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
                        from __ in Context.Engine.RunAsync(newCommand).ToAsync().ToError()
                        from ___ in ResetDiskIdentifier(vhdPath)
                        select unit,
                    None: () =>
                        from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("New-VHD")
                            .AddParameter("Path", vhdPath)
                            .AddParameter("Dynamic")
                            .AddParameter("SizeBytes", driveSettings.DiskSettings.SizeBytesCreate)).ToAsync().ToError()
                        select unit)
            }
            select unit;

        private EitherAsync<Error, Unit> ResetDiskIdentifier(string path) =>
            from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Set-VHD")
                    .AddParameter("Path", path)
                    .AddParameter("ResetDiskIdentifier")
                    .AddParameter("Force"))
                .ToAsync().ToError()
            select unit;

        private EitherAsync<Error, Unit> UpdateVirtualDisk(
            HardDiskDriveStorageSettings driveSettings) =>
            from vhdPath in driveSettings.AttachPath.ToEitherAsync(Error.New(
                $"The path is missing for virtual disk {driveSettings.ControllerNumber},{driveSettings.ControllerLocation}"))
            // get disk
            from vhdInfos in Context.Engine.GetObjectsAsync<VhdInfo>(new PsCommandBuilder()
                    .AddCommand("Get-VHD").AddArgument(vhdPath))
                .ToError().ToAsync()
            from vhdInfo in vhdInfos.HeadOrLeft(Errors.SequenceEmpty).ToAsync()
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
                      .ToError().ToAsync()
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