using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Haipa.VmConfig;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Core;
using Haipa.VmManagement.Data.Full;
using Haipa.VmManagement.Storage;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace Haipa.VmManagement.Converging
{
    public class ConvergeDrives : ConvergeTaskBase
    {
        public ConvergeDrives(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(TypedPsObject<VirtualMachineInfo> vmInfo)
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
                    from plannedDriveStorageSettings in VMDriveStorageSettings.PlanDriveStorageSettings(Context.HostSettings, Context.Config, Context.StorageSettings).ToAsync()
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

        private Task<Either<PowershellFailure, Unit>> VirtualDisks(TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedDriveStorageSettings)
        {
            var plannedDiskSettings = plannedDriveStorageSettings
                .Where(x => x.Type == VirtualMachineDriveType.VHD || x.Type == VirtualMachineDriveType.SharedVHD)
                .Cast<VMDiskStorageSettings>().ToSeq();

            return (from currentDiskSettings in CurrentVMDiskStorageSettings.DetectDiskStorageSettings(Context.Engine, Context.HostSettings, vmInfo.Value.HardDrives)
                from _ in plannedDiskSettings.MapToEitherAsync(s => VirtualDisk(s, vmInfo, currentDiskSettings))
                select Unit.Default);

        }


        private static Task<Either<PowershellFailure, Unit>> SetVMCheckpointType(TypedPsObject<VirtualMachineInfo> vmInfo, CheckpointType checkpointType, IPowershellEngine engine)
        {
            return engine.RunAsync(new PsCommandBuilder()
                .AddCommand("Set-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("CheckpointType", checkpointType));

        }

        private Task<Either<PowershellFailure, Unit>> DetachUndefinedDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedStorageSettings)

        {

            return (from currentDiskSettings in CurrentVMDiskStorageSettings.DetectDiskStorageSettings(Context.Engine, Context.HostSettings, vmInfo.Value.HardDrives).ToAsync()
                from _ in DetachUndefinedHardDrives(vmInfo, plannedStorageSettings, currentDiskSettings).ToAsync()
                from __ in DetachUndefinedDvdDrives(vmInfo, plannedStorageSettings).ToAsync()
                select Unit.Default).ToEither();
        }


        private Task<Either<PowershellFailure, Unit>> DetachUndefinedHardDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedStorageSettings,
            Seq<CurrentVMDiskStorageSettings> currentDiskStorageSettings)

        {
            var planedDiskSettings = plannedStorageSettings.Where(x =>
                    x.Type == VirtualMachineDriveType.VHD || x.Type == VirtualMachineDriveType.SharedVHD)
                .Cast<VMDiskStorageSettings>().ToSeq();

            var attachedPaths = planedDiskSettings.Map(s => s.AttachPath).Map(x => x.IfNone(""))
                .Where(x => !string.IsNullOrWhiteSpace(x));

            var frozenDiskIds = currentDiskStorageSettings.Where(x => x.Frozen).Map(x => x.AttachedVMId);

            return ConvergeHelpers.FindAndApply(vmInfo,
                    i => i.HardDrives,
                    hd =>
                    {
                        var plannedDiskAtControllerPos = planedDiskSettings
                            .FirstOrDefault(x =>
                                x.ControllerLocation == hd.ControllerLocation && x.ControllerNumber == hd.ControllerNumber);

                        var detach = plannedDiskAtControllerPos == null;

                        if (!detach && plannedDiskAtControllerPos.AttachPath.IsSome)
                        {
                            var plannedAttachPath = plannedDiskAtControllerPos.AttachPath.IfNone("");
                            if (hd.Path == null || !hd.Path.Equals(plannedAttachPath, StringComparison.InvariantCultureIgnoreCase))
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
                        {
                            Context.ReportProgress(hd.Path != null
                                ? $"Detaching disk {Path.GetFileNameWithoutExtension(hd.Path)} from controller: {hd.ControllerNumber}, Location: {hd.ControllerLocation}"
                                : $"Detaching unknown disk at controller: {hd.ControllerNumber}, Location: {hd.ControllerLocation}");
                        }

                        return detach;
                    },
                    i => Context.Engine.RunAsync(PsCommandBuilder.Create().AddCommand("Remove-VMHardDiskDrive")
                        .AddParameter("VMHardDiskDrive", i.PsObject))).Map(x => x.Lefts().HeadOrNone())
                .MatchAsync(
                    Some: l => Prelude.LeftAsync<PowershellFailure, Unit>(l).ToEither(),
                    None: () => Prelude.RightAsync<PowershellFailure, Unit>(Unit.Default)
                        .ToEither());

        }


        private Task<Either<PowershellFailure, Unit>> DetachUndefinedDvdDrives(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDriveStorageSettings> plannedStorageSettings)

        {

            var controllersAndLocations = plannedStorageSettings.Where(x => x.Type == VirtualMachineDriveType.DVD)
                .Map(x => new { x.ControllerNumber, x.ControllerLocation })
                .GroupBy(x => x.ControllerNumber)
                .ToImmutableDictionary(x => x.Key, x => ListExtensions.Map(x, y => y.ControllerLocation).ToImmutableArray());


            return ConvergeHelpers.FindAndApply(vmInfo,
                    i => i.DVDDrives,
                    hd =>
                    {

                        //ignore cloud init drive, will be handled later
                        if (hd.ControllerLocation == 63 && hd.ControllerNumber == 0)
                            return false;

                        var detach = !controllersAndLocations.ContainsKey(hd.ControllerNumber) ||
                                     !controllersAndLocations[hd.ControllerNumber].Contains(hd.ControllerLocation);

                        return detach;
                    },
                    i => Context.Engine.RunAsync(PsCommandBuilder.Create().AddCommand("Remove-VMDvdDrive")
                        .AddParameter("VMDvdDrive", i.PsObject))).Map(x => x.Lefts().HeadOrNone())
                .MatchAsync(
                    Some: l => Prelude.LeftAsync<PowershellFailure, Unit>(l).ToEither(),
                    None: () => Prelude.RightAsync<PowershellFailure, Unit>(Unit.Default)
                        .ToEither());

        }


        private async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> VirtualDisk(
            VMDiskStorageSettings diskSettings,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<CurrentVMDiskStorageSettings> currentStorageSettings)
        {

            var currentSettings =
                currentStorageSettings.Find(x => diskSettings.Path.Equals(x.Path, StringComparison.OrdinalIgnoreCase)
                                                 && diskSettings.Name.Equals(x.Name, StringComparison.InvariantCultureIgnoreCase));
            var frozenOptional = currentSettings.Map(x => x.Frozen);

            if (frozenOptional.IsSome && frozenOptional.ValueUnsafe())
            {
                await Context.ReportProgress($"Skipping disk '{diskSettings.Name}': storage management is disabled for this disk.").ConfigureAwait(false);
                return vmInfo;
            }



            return await diskSettings.AttachPath.Map(async (vhdPath) =>
            {

                if (!File.Exists(vhdPath))
                {
                    await Context.ReportProgress($"Create VHD: {diskSettings.Name}").ConfigureAwait(false);

                    var createDiskResult = await diskSettings.ParentPath.Match(Some: parentPath =>
                        {
                            return Context.Engine.RunAsync(PsCommandBuilder.Create().Script(
                                $"New-VHD -Path \"{vhdPath}\" -ParentPath \"{parentPath}\" -Differencing"));
                        },
                        None: () =>
                        {
                            return Context.Engine.RunAsync(PsCommandBuilder.Create().Script(
                                $"New-VHD -Path \"{vhdPath}\" -Dynamic -SizeBytes {diskSettings.SizeBytes}"));
                        });

                    if (createDiskResult.IsLeft)
                        return Prelude.Left(createDiskResult.LeftAsEnumerable().FirstOrDefault());
                }

                var sizeResult = await Context.Engine
                    .GetObjectsAsync<VhdInfo>(new PsCommandBuilder().AddCommand("get-vhd").AddArgument(vhdPath))
                    .BindAsync(x => x.HeadOrLeft(new PowershellFailure())).BindAsync(async (vhdInfo) =>
                    {
                        if (vhdInfo.Value.Size != diskSettings.SizeBytes && diskSettings.SizeBytes > 0)
                        {
                            var gb = Math.Round(diskSettings.SizeBytes / 1024d / 1024 / 1024, 1);
                            await Context.ReportProgress(
                                $"Resizing disk {diskSettings.Name} to {gb} GB");
                            return await Context.Engine.RunAsync(PsCommandBuilder.Create().AddCommand("Resize-VHD")
                                .AddArgument(vhdPath)
                                .AddParameter("Size", diskSettings.SizeBytes));

                        }

                        return Unit.Default;
                    });

                if (sizeResult.IsLeft)
                    return Prelude.Left(sizeResult.LeftAsEnumerable().FirstOrDefault());


                return await ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
                        i => i.HardDrives,
                        disk => currentSettings.Map(x => x.AttachedVMId) == disk.Id,
                        async () =>
                        {
                            await Context.ReportProgress($"Attaching disk {diskSettings.Name} to controller: {diskSettings.ControllerNumber}, Location: {diskSettings.ControllerLocation}").ConfigureAwait(false);
                            return (await Context.Engine.GetObjectsAsync<HardDiskDriveInfo>(PsCommandBuilder.Create()
                                .AddCommand("Add-VMHardDiskDrive")
                                .AddParameter("VM", vmInfo.PsObject)
                                .AddParameter("Path", vhdPath)
                                .AddParameter("ControllerNumber", diskSettings.ControllerNumber)
                                .AddParameter("ControllerLocation", diskSettings.ControllerLocation)
                                .AddParameter("PassThru")
                            ).ConfigureAwait(false));

                        }).BindAsync(_ => vmInfo.RecreateOrReload(Context.Engine))


                    .ConfigureAwait(false);

            }).IfNone(vmInfo.RecreateOrReload(Context.Engine));

        }


    }
}