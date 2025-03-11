using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.OVN.Windows;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.GenePool.Model;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;


namespace Eryph.VmManagement
{
    public static class VirtualMachine
    {

        public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Create(
            IPowershellEngine engine,
            string vmName,
            string storageIdentifier,
            string vmPath,
            int? startupMemory)
        {
            var memoryStartupBytes = startupMemory.GetValueOrDefault(EryphConstants.DefaultCatletMemoryMb) * 1024L * 1024;

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
                .ToAsync()
                .ToError()
                .Bind(info => Rename(engine, info, vmName))
                .Bind(info => SetDefaults(engine, info));

        }

        //keep this method (old template import) as base for vm import feature
        public static EitherAsync<Error, TypedPsObject<PlannedVirtualMachineInfo>> VMTemplateFromPath(
            IPowershellEngine engine,
            string vmPath)
        {
            if (string.IsNullOrEmpty(vmPath))
                return LeftAsync<Error, TypedPsObject<PlannedVirtualMachineInfo>>(
                    Error.New("Cannot create catlet from vm path - path is missing."));

            var configRootPath = Path.Combine(vmPath, "Virtual Machines");

            var vmInfo = Directory.GetFiles(configRootPath, "*.vmcx")
                .HeadOrLeft(Error.New("Failed to find VM configuration file")).ToAsync()
                .Bind(configPath =>
                    engine.GetObjectsAsync<VMCompatibilityReportInfo>(PsCommandBuilder.Create()
                            .AddCommand("Compare-VM")
                            .AddParameter("Path", configPath)
                        ).ToError().ToAsync()
                        .Bind(x => x.HeadOrLeft(Error.New("Failed to load VM from path")).ToAsync())
                        .Bind(rep => ExpandTemplateData(
                            rep.GetProperty(x => x.VM), engine)));

            return vmInfo;
        }

        //keep this method (old template import) as base for vm import feature
        public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ImportTemplate(
            IPowershellEngine engine,
            string vmName,
            string storageIdentifier,
            VMStorageSettings storageSettings,
            PlannedVirtualMachineInfo template)
        {
            var configPath = Path.Combine(template.Path, "Virtual Machines", $"{template.Id}.vmcx");

            var vmStorePath = Path.Combine(storageSettings.VMPath, storageIdentifier);
            var vhdPath = Path.Combine(storageSettings.DefaultVhdPath, storageIdentifier);

            var vmInfo = engine.GetObjectsAsync<VMCompatibilityReportInfo>(PsCommandBuilder.Create()
                    .AddCommand("Compare-VM")
                    .AddParameter("VirtualMachinePath", vmStorePath)
                    .AddParameter("SnapshotFilePath", vmStorePath)
                    .AddParameter("Path", configPath)
                    .AddParameter("VhdDestinationPath", vhdPath)
                    .AddParameter("Copy")
                    .AddParameter("GenerateNewID")
                ).ToError().ToAsync()
                .Bind(x => x.HeadOrLeft(Error.New("Failed to Import VM")).ToAsync())
                .Bind(rep => (
                        //from uD in RemoveAllPlannedDrives(engine, rep.GetProperty(x => x.VM))
                        //from _ in Rename(engine, rep.GetProperty(x => x.VM), vmName)
                        from __ in ResetMetadata(engine, rep.GetProperty(x => x.VM))
                        from ___ in RenamePlannedNetAdaptersToConvention(engine, rep.GetProperty(x => x.VM))
                        from ____ in DisconnectNetworkAdapters(engine, rep.GetProperty(x => x.VM))
                        from repRecreated in RightAsync<Error, TypedPsObject<VMCompatibilityReportInfo>>(
                            rep.Recreate())
                        select repRecreated
                    )
                )
                .Apply(repEither =>
                    from rep in repEither
                    //from template in ExpandTemplateData(rep.Value.VM, engine)
                    from vms in engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                        .AddCommand("Import-VM")
                        .AddParameter("CompatibilityReport", rep.PsObject)).ToError().ToAsync()
                    from vm in vms.HeadOrLeft(Error.New("Failed to import VM Image")).ToAsync()
                    from _ in RenameDisksToConvention(engine, vm).ToAsync()
                    from vmReloaded in vm.Reload(engine)
                    select vmReloaded);


            return vmInfo;
        }

        public static EitherAsync<Error, TypedPsObject<PlannedVirtualMachineInfo>> RemoveAllPlannedDrives(
            IPowershellEngine engine,
            TypedPsObject<PlannedVirtualMachineInfo> vmInfo)
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddInput(vmInfo.GetList(x=>x.HardDrives)
                    .Select(x=>x.PsObject).ToArray())
                .AddCommand("Remove-VMHardDiskDrive")
            ).ToError().ToAsync().Bind(u => vmInfo.RecreateOrReload(engine));
        }


        public static EitherAsync<Error, TypedPsObject<T>> Rename<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo,
            string newName)
            where T : IVirtualMachineCoreInfo
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Rename-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("NewName", newName)
            ).ToError().ToAsync().Bind(u => vmInfo.RecreateOrReload(engine));
        }

        public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> SetDefaults(
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            return
                from setVMCommand in engine.GetObjectsAsync<PowershellCommand>(
                        new PsCommandBuilder().AddCommand("Get-Command")
                            .AddArgument("Set-VM")).ToAsync()
                    .ToError()
                    .Bind(o => 
                        o.HeadOrLeft(Error.New("Command Set-VM not found")).ToAsync())
                let builder = BuildSetVMCommand(vmInfo, setVMCommand)
                from uSet in 
                engine.RunAsync(builder).ToAsync().ToError()
                from reloaded in vmInfo.RecreateOrReload(engine)
                select reloaded;

            static PsCommandBuilder BuildSetVMCommand(TypedPsObject<VirtualMachineInfo> vmInfo, PowershellCommand commandInfo)
            {
                var builder = new PsCommandBuilder().AddCommand("Set-VM");
                builder
                    .AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("DynamicMemory", false)
                    .AddParameter("AutomaticStartAction", "Nothing")
                    .AddParameter("AutomaticStopAction", "Save");

                if (commandInfo.Parameters.ContainsKey("AutomaticCheckpointsEnabled"))
                    builder.AddParameter("AutomaticCheckpointsEnabled", false);

                if (commandInfo.Parameters.ContainsKey("EnhancedSessionTransportType"))
                    builder.AddParameter("EnhancedSessionTransportType", "VMBus");

                return builder;
            }
        }

        public static Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
            VmHostAgentConfiguration vmHostAgentConfig,
            VMHostMachineData hostInfo,
            IPowershellEngine engine,
            IHyperVOvsPortManager portManager,
            Func<string, Task> reportProgress,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            CatletConfig machineConfig,
            CatletMetadata metadata,
            MachineNetworkSettings[] networkSetting,
            VMStorageSettings storageSettings,
            Seq<UniqueGeneIdentifier> resolvedGenes)
        {
            var convergeContext = new ConvergeContext(
                vmHostAgentConfig, engine, portManager, reportProgress, machineConfig, 
                metadata, storageSettings, networkSetting, hostInfo, resolvedGenes);

            var convergeTasks = new ConvergeTaskBase[]
            {
                new ConvergeSecureBoot(convergeContext),
                new ConvergeTpm(convergeContext),
                new ConvergeCPU(convergeContext),
                new ConvergeNestedVirtualization(convergeContext),
                new ConvergeMemory(convergeContext),
                new ConvergeDrives(convergeContext),
                new ConvergeNetworkAdapters(convergeContext),
            };

            return convergeTasks.Fold(
                RightAsync<Error, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither(),
                (info, task) => info.BindAsync(task.Converge));
        }

        public static Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> ConvergeConfigDrive(
            VmHostAgentConfiguration vmHostAgentConfig,
            VMHostMachineData hostInfo,
            IPowershellEngine engine,
            IHyperVOvsPortManager portManager,
            Func<string, Task> reportProgress,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            CatletConfig machineConfig,
            CatletMetadata metadata,
            VMStorageSettings storageSettings)
        {
            // Pass empty MachineNetworkSettings as converging the cloud init disk
            // does not require them.
            var convergeContext = new ConvergeContext(
                vmHostAgentConfig, engine, portManager, reportProgress, machineConfig,
                metadata, storageSettings, [], hostInfo, Empty);

            var convergeTasks = new ConvergeTaskBase[]
            {
                new ConvergeCloudInitDisk(convergeContext),
            };

            return convergeTasks.Fold(
                RightAsync<Error, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither(),
                (info, task) => info.BindAsync(task.Converge));
        }


        private static EitherAsync<Error, TypedPsObject<T>> ResetMetadata<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo)
            where T : IVirtualMachineCoreInfo
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Notes", "")
            ).ToAsync().ToError().Bind(u => vmInfo.RecreateOrReload(engine));
        }

        private static EitherAsync<Error, TypedPsObject<PlannedVirtualMachineInfo>> ExpandTemplateData(
            TypedPsObject<PlannedVirtualMachineInfo> template, IPowershellEngine engine)
        {
            return template.GetList(x=>x.HardDrives)
                .Map(device =>
                    from hd in device.CastSafeAsync<PlannedHardDiskDriveInfo>().ToError().ToAsync()
                    from optionalDrive in VhdQuery.GetVhdInfo(engine, hd.Value.Path)
                    from drive in optionalDrive.ToEitherAsync(Error.New("Failed to find realized VM disk"))
                    let _ = drive.Apply(d => hd.Value.Size = d.Value.Size)
                    select hd)
                .SequenceSerial()
                .Map(_ => template);
        }

        private static EitherAsync<Error, TypedPsObject<T>> DisconnectNetworkAdapters<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo)
            where T : IVirtualMachineCoreInfo
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Get-VMNetworkAdapter")
                .AddParameter("VM", vmInfo.PsObject)
                .AddCommand("Disconnect-VMNetworkAdapter")
            ).ToError().ToAsync().Bind(u => vmInfo.RecreateOrReload(engine));
        }

        private static async Task<Either<Error, Unit>> RenameDisksToConvention<T>(
            IPowershellEngine engine,
            TypedPsObject<T> vmInfo)
            where T : IVMWithDrivesInfo
        {
            const string abc = "abcdefklmnopqrstvxyz";

            var counterSCSI = -1;
            var counterIDE = -1;

            vmInfo.GetList(x => x.HardDrives).Map(device =>
            {
                var disk = device.Cast<HardDiskDriveInfo>();
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


        private static EitherAsync<Error, Unit> RenamePlannedNetAdaptersToConvention(
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
            }).ToError().MapAsync(seq => Unit.Default).ToAsync();
        }
    }
}