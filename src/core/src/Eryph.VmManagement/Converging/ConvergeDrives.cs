using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using LanguageExt.UnsafeValueAccess;
using Prelude = LanguageExt.Prelude;
using TaskExtensions = LanguageExt.TaskExtensions;
using Unit = LanguageExt.Unit;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeDrives : ConvergeTaskBase
    {
        public ConvergeDrives(ConvergeContext context) : base(context)
        {
        }

        public override async Task<LanguageExt.Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            if (Context.StorageSettings.Frozen)
                return Prelude.Right<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo);

            var currentCheckpointType = vmInfo.Value.CheckpointType;

            try
            {
                await (
                    //prevent snapshots creating during running disk converge
                    from _ in SetVMCheckpointType(vmInfo, CheckpointType.Disabled, Context.Engine).ToAsync()
                    //make a plan
                    from plannedDriveStorageSettings in VMDriveStorageSettings
                        .PlanDriveStorageSettings(Context.HostSettings, Context.Config, Context.StorageSettings)
                        .ToAsync()
                    //ensure that the changes reflect the current VM settings
                    from infoReloaded in vmInfo.Reload(Context.Engine).ToAsync()
                    //detach removed disks
                    from __ in DetachUndefinedDrives(infoReloaded, plannedDriveStorageSettings).ToAsync()
                    from infoRecreated in vmInfo.RecreateOrReload(Context.Engine).ToAsync()
                    from ___ in VirtualDisks(infoRecreated, plannedDriveStorageSettings).ToAsync()
                    select Unit.Default).ToEither().ConfigureAwait(false);
            }
            finally
            {
                await SetVMCheckpointType(vmInfo, currentCheckpointType, Context.Engine).ConfigureAwait(false);
            }

            return await vmInfo.Reload(Context.Engine).ConfigureAwait(false);
        }

        private Task<LanguageExt.Either<PowershellFailure, Unit>> VirtualDisks(TypedPsObject<VirtualMachineInfo> vmInfo,
            LanguageExt.Seq<VMDriveStorageSettings> plannedDriveStorageSettings)
        {
            var plannedDiskSettings = plannedDriveStorageSettings
                .Where(x => x.Type == VirtualMachineDriveType.VHD || x.Type == VirtualMachineDriveType.SharedVHD)
                .Cast<HardDiskDriveStorageSettings>().ToSeq();

            return (from currentDiskSettings in CurrentHardDiskDriveStorageSettings.Detect(Context.Engine,
                    Context.HostSettings, vmInfo.GetList(x=>x.HardDrives)).ToAsync()
                from _ in plannedDiskSettings.MapToEitherAsync(s => VirtualDisk(s, vmInfo, currentDiskSettings))
                    .ToAsync()
                select Unit.Default).ToEither();
        }


        private static Task<LanguageExt.Either<PowershellFailure, Unit>> SetVMCheckpointType(
            TypedPsObject<VirtualMachineInfo> vmInfo, CheckpointType checkpointType, IPowershellEngine engine)
        {
            return engine.RunAsync(new PsCommandBuilder()
                .AddCommand("Set-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("CheckpointType", checkpointType));
        }

        private Task<LanguageExt.Either<PowershellFailure, Unit>> DetachUndefinedDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo, LanguageExt.Seq<VMDriveStorageSettings> plannedStorageSettings)

        {
            return (from currentDiskSettings in CurrentHardDiskDriveStorageSettings
                    .Detect(Context.Engine, Context.HostSettings, vmInfo.GetList(x=>x.HardDrives)).ToAsync()
                from _ in DetachUndefinedHardDrives(vmInfo, plannedStorageSettings, currentDiskSettings).ToAsync()
                from __ in DetachUndefinedDvdDrives(vmInfo, plannedStorageSettings).ToAsync()
                select Unit.Default).ToEither();
        }


        private Task<LanguageExt.Either<PowershellFailure, Unit>> DetachUndefinedHardDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo, LanguageExt.Seq<VMDriveStorageSettings> plannedStorageSettings,
            LanguageExt.Seq<CurrentHardDiskDriveStorageSettings> currentDiskStorageSettings)

        {
            var planedDiskSettings = plannedStorageSettings.Where(x =>
                    x.Type == VirtualMachineDriveType.VHD || x.Type == VirtualMachineDriveType.SharedVHD)
                .Cast<HardDiskDriveStorageSettings>().ToSeq();

            var attachedPaths = planedDiskSettings.Map(s => s.AttachPath).Map(x => x.IfNone(""))
                .Where(x => !string.IsNullOrWhiteSpace(x));

            var frozenDiskIds = currentDiskStorageSettings.Where(x => x.Frozen).Map(x => x.AttachedVMId);

            return TaskExtensions.Map(ConvergeHelpers.FindAndApply(vmInfo,
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
                                StringComparison.InvariantCultureIgnoreCase))
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
                        .AddParameter("VMHardDiskDrive", i.PsObject))), x => x.Lefts().HeadOrNone())
                .MatchAsync(
                    l => Prelude.LeftAsync<PowershellFailure, Unit>(l).ToEither(),
                    () => Prelude.RightAsync<PowershellFailure, Unit>(Unit.Default)
                        .ToEither());
        }


        private Task<LanguageExt.Either<PowershellFailure, Unit>> DetachUndefinedDvdDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo, LanguageExt.Seq<VMDriveStorageSettings> plannedStorageSettings)

        {
            var controllersAndLocations = plannedStorageSettings.Where(x => x.Type == VirtualMachineDriveType.DVD)
                .Map(x => new {x.ControllerNumber, x.ControllerLocation})
                .GroupBy(x => x.ControllerNumber)
                .ToImmutableDictionary(x => x.Key, x => x.Map(y => y.ControllerLocation).ToImmutableArray());


            return TaskExtensions.Map(ConvergeHelpers.FindAndApply(vmInfo,
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
                        .AddParameter("VMDvdDrive", i.PsObject))), x => x.Lefts().HeadOrNone())
                .MatchAsync(
                    l => Prelude.LeftAsync<PowershellFailure, Unit>(l).ToEither(),
                    () => Prelude.RightAsync<PowershellFailure, Unit>(Unit.Default)
                        .ToEither());
        }


        private async Task<LanguageExt.Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> VirtualDisk(
            HardDiskDriveStorageSettings driveSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            LanguageExt.Seq<CurrentHardDiskDriveStorageSettings> currentStorageSettings)
        {
            var currentSettings =
                currentStorageSettings.Find(x =>
                    driveSettings.DiskSettings.Path.Equals(x.DiskSettings.Path, StringComparison.OrdinalIgnoreCase)
                    && driveSettings.DiskSettings.Name.Equals(x.DiskSettings.Name,
                        StringComparison.InvariantCultureIgnoreCase));
            var frozenOptional = currentSettings.Map(x => x.Frozen);

            if (frozenOptional.IsSome && frozenOptional.ValueUnsafe())
            {
                await Context
                    .ReportProgress(
                        $"Skipping HD Drive '{driveSettings.DiskSettings.Name}': storage management is disabled for this disk.")
                    .ConfigureAwait(false);
                return vmInfo;
            }


            return await driveSettings.AttachPath.Map(async vhdPath =>
            {
                if (!File.Exists(vhdPath))
                {
                    await Context.ReportProgress($"Creating HD Drive: {driveSettings.DiskSettings.Name}")
                        .ConfigureAwait(false);

                    var createDiskResult = await driveSettings.DiskSettings.ParentSettings.Match(parentSettings =>
                        {
                            var parentFilePath = Path.Combine(parentSettings.Path, parentSettings.FileName);
                            return Context.Engine.RunAsync(PsCommandBuilder.Create().Script(
                                $"New-VHD -Path \"{vhdPath}\" -ParentPath \"{parentFilePath}\" -Differencing"));
                        },
                        () =>
                        {
                            return Context.Engine.RunAsync(PsCommandBuilder.Create().Script(
                                $"New-VHD -Path \"{vhdPath}\" -Dynamic -SizeBytes {driveSettings.DiskSettings.SizeBytes}"));
                        });

                    if (createDiskResult.IsLeft)
                        return Prelude.Left(createDiskResult.LeftAsEnumerable().FirstOrDefault());
                }

                var sizeResult = await Context.Engine
                    .GetObjectsAsync<VhdInfo>(new PsCommandBuilder().AddCommand("get-vhd").AddArgument(vhdPath))
                    .BindAsync(x => x.HeadOrLeft(new PowershellFailure())).BindAsync(async vhdInfo =>
                    {
                        if (vhdInfo.Value.Size != driveSettings.DiskSettings.SizeBytes &&
                            driveSettings.DiskSettings.SizeBytes > 0)
                        {
                            var gb = Math.Round(driveSettings.DiskSettings.SizeBytes / 1024d / 1024 / 1024, 1);
                            await Context.ReportProgress(
                                $"Resizing disk {driveSettings.DiskSettings.Name} to {gb} GB");
                            return await Context.Engine.RunAsync(PsCommandBuilder.Create().AddCommand("Resize-VHD")
                                .AddArgument(vhdPath)
                                .AddParameter("Size", driveSettings.DiskSettings.SizeBytes));
                        }

                        return Unit.Default;
                    });

                if (sizeResult.IsLeft)
                    return Prelude.Left(sizeResult.LeftAsEnumerable().FirstOrDefault());


                return await ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
                        i => i.HardDrives,
                        device => device.Cast<HardDiskDriveInfo>()
                                .Map(disk => currentSettings.Map(x => x.AttachedVMId) == disk.Id),
                        async () =>
                        {
                            await Context
                                .ReportProgress(
                                    $"Attaching HD Drive {driveSettings.DiskSettings.Name} to controller: {driveSettings.ControllerNumber}, Location: {driveSettings.ControllerLocation}")
                                .ConfigureAwait(false);
                            return await Context.Engine.GetObjectsAsync<VirtualMachineDeviceInfo>(PsCommandBuilder.Create()
                                .AddCommand("Add-VMHardDiskDrive")
                                .AddParameter("VM", vmInfo.PsObject)
                                .AddParameter("Path", vhdPath)
                                .AddParameter("ControllerNumber", driveSettings.ControllerNumber)
                                .AddParameter("ControllerLocation", driveSettings.ControllerLocation)
                                .AddParameter("PassThru")
                            ).ConfigureAwait(false);
                        }).BindAsync(_ => vmInfo.RecreateOrReload(Context.Engine))
                    .ConfigureAwait(false);
            }).IfNone(vmInfo.RecreateOrReload(Context.Engine));
        }
    }
}