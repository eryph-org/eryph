using System;
using System.IO;
using System.Threading.Tasks;
using Haipa.Messages.Events;
using Haipa.VmConfig;
using Haipa.VmManagement.Converging;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Core;
using Haipa.VmManagement.Data.Full;
using Haipa.VmManagement.Data.Planned;
using Haipa.VmManagement.Storage;
using LanguageExt;
using LanguageExt.SomeHelp;
using static LanguageExt.Prelude;


namespace Haipa.VmManagement
{
    public static class VirtualMachine
    {
        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> ImportTemplate(
            IPowershellEngine engine,
            HostSettings hostSettings,
            string vmName,
            string storageIdentifier,
            string vmPath,
            PlannedVirtualMachineInfo template)
        {

            var configPath = Path.Combine(template.Path, "Virtual Machines", $"{template.Id}.vmcx");

            var vmStorePath = Path.Combine(vmPath, storageIdentifier);
            var vhdPath = Path.Combine(hostSettings.DefaultVirtualHardDiskPath, storageIdentifier);

            var vmInfo = engine.GetObjectsAsync<VMCompatibilityReportInfo>(PsCommandBuilder.Create()
                    .AddCommand("Compare-VM")
                    .AddParameter("VirtualMachinePath", vmStorePath)
                    .AddParameter("SnapshotFilePath", vmStorePath)
                    .AddParameter("Path", configPath)
                    .AddParameter("VhdDestinationPath", vhdPath)
                    .AddParameter("Copy")
                    .AddParameter("GenerateNewID")
                )
                .BindAsync(x => x.HeadOrLeft(new PowershellFailure { Message = "Failed to Import VM Image" }))
                .BindAsync(rep => (
                        from _ in Rename(engine, rep.GetProperty(x => x.VM), vmName).ToAsync()
                        from __ in ResetMetadata(engine, rep.GetProperty(x => x.VM)).ToAsync()
                        from ___ in RenamePlannedNetAdaptersToConvention(engine, rep.GetProperty(x => x.VM)).ToAsync()
                        from ____ in DisconnectNetworkAdapters(engine, rep.GetProperty(x => x.VM)).ToAsync()
                        from repRecreated in RightAsync<PowershellFailure, TypedPsObject<VMCompatibilityReportInfo>>(
                            rep.Recreate())
                        select repRecreated
                    ).ToEither()

                )
                .Apply(repEither =>
                    from rep in repEither
                    //from template in ExpandTemplateData(rep.Value.VM, engine)
                    from vms in engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                            .AddCommand("Import-VM")
                            .AddParameter("CompatibilityReport", rep.PsObject))
                    from vm in vms.HeadOrLeft(new PowershellFailure { Message = "Failed to import VM Image" }).ToAsync().ToEither()
                    from _ in RenameDisksToConvention(engine, vm)
                    from vmReloaded in vm.Reload(engine)
                    select vmReloaded);


            return vmInfo;
        }

        public static Task<Either<PowershellFailure, Option<PlannedVirtualMachineInfo>>> TemplateFromImage(
            IPowershellEngine engine,
            HostSettings hostSettings,
            MachineImageConfig imageConfig)
        {

            if(string.IsNullOrWhiteSpace(imageConfig?.Name))
                return RightAsync<PowershellFailure, Option<PlannedVirtualMachineInfo>>(None).ToEither();
            

            var imageRootPath = Path.Combine(hostSettings.DefaultVirtualHardDiskPath, "Images");
            var imagePath = Path.Combine(imageRootPath,
                $"{imageConfig.Name}\\{imageConfig.Tag}\\");

            var configRootPath = Path.Combine(imagePath, "Virtual Machines");

            var vmInfo = Directory.GetFiles(configRootPath, "*.vmcx")
                .HeadOrLeft(new PowershellFailure {Message = "Failed to find image configuration file"}).AsTask()
                .BindAsync(configPath =>
                    engine.GetObjectsAsync<VMCompatibilityReportInfo>(PsCommandBuilder.Create()
                            .AddCommand("Compare-VM")
                            .AddParameter("Path", configPath)
                        )

                .BindAsync(x => x.HeadOrLeft(new PowershellFailure {Message = "Failed to load VM Image"}))
                .BindAsync(rep=> ExpandTemplateData(rep.Value.VM, engine)));

            return vmInfo.MapAsync(Some);
        }

        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Create(
            IPowershellEngine engine,
            string vmName,
            string storageIdentifier,
            string vmPath,
            int? startupMemory)
        {
            var memoryStartupBytes = startupMemory.GetValueOrDefault(1024) * 1024L * 1024;

            return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("New-VM")
                    .AddParameter("Name", storageIdentifier)
                    .AddParameter("Path", vmPath)
                    .AddParameter("MemoryStartupBytes", memoryStartupBytes)
                    .AddParameter("Generation", 2))
                .MapAsync(x => x.Head).MapAsync(
                    async result =>
                    {

                        await engine.RunAsync(PsCommandBuilder.Create().AddCommand("Get-VMNetworkAdapter")
                            .AddParameter("VM", result.PsObject).AddCommand("Remove-VMNetworkAdapter"));


                        return result;
                    })
                .BindAsync(info => Rename(engine, info, vmName));

        }



        public static Task<Either<PowershellFailure, TypedPsObject<T>>> Rename<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo,
            string newName)
            where T : IVirtualMachineCoreInfo
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Rename-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("NewName", newName)
            ).BindAsync(u => vmInfo.RecreateOrReload(engine));

        }

        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
            HostSettings hostSettings,
            IPowershellEngine engine,
            Func<string, Task> reportProgress,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            MachineConfig machineConfig,
            VMStorageSettings storageSettings)
        {
            var convergeContext = new ConvergeContext(hostSettings, engine, reportProgress, machineConfig, storageSettings);

            var convergeTasks = new ConvergeTaskBase[]
            {
                new ConvergeFirmware(convergeContext),
                new ConvergeCPU(convergeContext),
                new ConvergeDrives(convergeContext),
                new ConvergeNetworkAdapters(convergeContext),
                new ConvergeCloudInitDisk(convergeContext),

            };

            return convergeTasks.Fold(RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither(),
                (info, task) => info.BindAsync(task.Converge));

        }


        private static Task<Either<PowershellFailure, TypedPsObject<T>>> ResetMetadata<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo)
            where T : IVirtualMachineCoreInfo
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Notes", "")
            ).BindAsync(u => vmInfo.RecreateOrReload(engine));

        }

        private static Task<Either<PowershellFailure, PlannedVirtualMachineInfo>> ExpandTemplateData(PlannedVirtualMachineInfo template, IPowershellEngine engine)
        {

            return template.HardDrives.ToSeq().MapToEitherAsync(hd =>
                    (from optionalDrive in VhdQuery.GetVhdInfo(engine, hd.Path).ToAsync()
                        from drive in optionalDrive.ToEither(new PowershellFailure { Message = "Failed to find realized image disk" })
                            .ToAsync()
                        let _ = drive.Apply(d => hd.Size = d.Value.Size)
                        select hd).ToEither())

                .MapT(hd => template);

        }

        private static Task<Either<PowershellFailure, TypedPsObject<T>>> DisconnectNetworkAdapters<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo)
            where T : IVirtualMachineCoreInfo
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Get-VMNetworkAdapter")
                .AddParameter("VM", vmInfo.PsObject)
                .AddCommand("Disconnect-VMNetworkAdapter")
            ).BindAsync(u => vmInfo.RecreateOrReload(engine));

        }

        private static async Task<Either<PowershellFailure, Unit>> RenameDisksToConvention<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo)
            where T : IVMWithDrivesInfo<HardDiskDriveInfo>
        {
            const string abc = "abcdefklmnopqrstvxyz";

            var counterSCSI = -1;
            var counterIDE = -1;

            vmInfo.GetList(x => x.HardDrives).Map(disk =>
            {
                var fileName = Path.GetFileNameWithoutExtension("filename");

                switch (disk.Value.ControllerType)
                {
                    case ControllerType.SCSI:
                        counterSCSI++;
                        fileName = "sd" + abc[counterSCSI];
                        break;
                    case ControllerType.IDE:
                        counterIDE++;
                        fileName = "hd" + abc[counterIDE];
                        break;
                }

                var newPath = Path.Combine(Path.GetDirectoryName(disk.Value.Path), $"{fileName}.vhdx");
                File.Move(disk.Value.Path, newPath);
                return engine.Run(new PsCommandBuilder().AddCommand("Set-VMHardDiskDrive")
                    .AddParameter("VMHardDiskDrive", disk.PsObject).AddParameter("Path", newPath));
            }).ToArray();

            return Unit.Default;
        }


        private static Task<Either<PowershellFailure, Unit>> RenamePlannedNetAdaptersToConvention(
            IPowershellEngine engine,
            TypedPsObject<PlannedVirtualMachineInfo> vmInfo)
        {
            var adapterCounter = -1;

            return vmInfo.GetList(x => x.NetworkAdapters).MapToEitherAsync(adapter =>
            {
                adapterCounter++;
                var adapterName = "eth" + adapterCounter;

                return engine.RunAsync(new PsCommandBuilder()
                    .AddCommand("Rename-VMNetworkAdapter")
                    .AddParameter("VMNetworkAdapter", adapter.PsObject).AddParameter("NewName", adapterName));

            }).MapAsync(seq => Unit.Default);

        }




    }
}
