using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using Contiva.CloudInit.ConfigDrive.Generator;
using Contiva.CloudInit.ConfigDrive.NoCloud;
using Contiva.CloudInit.ConfigDrive.Processing;
using HyperVPlus.VmConfig;
using HyperVPlus.VmManagement.Data;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Newtonsoft.Json.Linq;

using static LanguageExt.Prelude;

namespace HyperVPlus.VmManagement
{
    public static class Converge
    {
        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Firmware(TypedPsObject<VirtualMachineInfo> vmInfo,
            VirtualMachineConfig config, IPowershellEngine engine, Func<string, Task> reportProgress)
        {
            if (vmInfo.Value.Generation < 2)
                return vmInfo;

            return await engine.GetObjectsAsync<VMFirmwareInfo>(PsCommandBuilder.Create()
                    .AddCommand("Get-VMFirmware")
                    .AddParameter("VM", vmInfo.PsObject))
                .BindAsync(firmwareInfoSeq =>
                    firmwareInfoSeq.HeadOrNone()
                        .Match<Either<PowershellFailure, TypedPsObject<VMFirmwareInfo>>>(
                            None: () => new PowershellFailure { Message = "Failed to get VM Firmware" },
                            Some: s => s
                        ))         
                .BindAsync(async firmwareInfo =>
                    {
                        if (firmwareInfo.Value.SecureBoot != OnOffState.Off)
                        {
                            await reportProgress($"Configure VM Firmware - Secure Boot: {OnOffState.Off}")
                                .ConfigureAwait(false);


                            var res= await engine.RunAsync(PsCommandBuilder.Create()
                                .AddCommand("Set-VMFirmware")
                                .AddParameter("VM", vmInfo.PsObject)
                                .AddParameter("EnableSecureBoot", OnOffState.Off)).MapAsync(
                                r => new TypedPsObject<VirtualMachineInfo>(vmInfo.PsObject)
                            ).ConfigureAwait(false);
                            return res;
                        }

                        return new TypedPsObject<VirtualMachineInfo>(vmInfo.PsObject);
                    }
                ).ConfigureAwait(false);

        }

        public static  async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Cpu(TypedPsObject<VirtualMachineInfo> vmInfo,
            Option<VirtualMachineCpuConfig> cpuConfig, IPowershellEngine engine, Func<string, Task> reportProgress)
        {
            return await cpuConfig.MatchAsync(
                None: () => vmInfo,
                Some: async config =>
                {
                    if (vmInfo.Value.ProcessorCount != config.Count)
                    {
                        await reportProgress($"Configure VM Processor: Count: {config.Count}").ConfigureAwait(false);

                        await engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Set-VMProcessor")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("Count", config.Count)).ConfigureAwait(false);

                        return new TypedPsObject<VirtualMachineInfo>(vmInfo.PsObject);
                    }

                    return vmInfo;
                }).ConfigureAwait(false);

        }


        public static async Task Definition(
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            VirtualMachineConfig vmConfig,
            Func<string, Task> reportProgress)
        {

            if (vmInfo.Value.Generation >= 2)
            {
                await engine.GetObjectsAsync<VMFirmwareInfo>(PsCommandBuilder.Create()
                    .AddCommand("Get-VMFirmware")
                    .AddParameter("VM", vmInfo.PsObject)).MapAsync(async (firmwareInfo) =>
                {
                    if (firmwareInfo.Head.Value.SecureBoot != OnOffState.Off)
                    {
                        await reportProgress($"Configure VM Firmware - Secure Boot: {OnOffState.Off}")
                            .ConfigureAwait(false);

                        await engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Set-VMFirmware")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("EnableSecureBoot", OnOffState.Off)).ConfigureAwait(false);


                    }
                }).ConfigureAwait(false);
            }


            if (vmInfo.Value.ProcessorCount != vmConfig.Cpu.Count)
            {
                await reportProgress($"Configure VM Processor: Count: {vmConfig.Cpu.Count}").ConfigureAwait(false);

                await engine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Set-VMProcessor")
                    .AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Count", vmConfig.Cpu.Count)).ConfigureAwait(false);
            }

            var memoryStartupBytes = vmConfig.Memory.Startup * 1024L * 1024;

            if (vmInfo.Value.MemoryStartup != memoryStartupBytes)
            {
                await reportProgress($"Configure VM Memory: Startup: {vmConfig.Memory.Startup} MB").ConfigureAwait(false);

                await engine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Set-VMMemory")
                    .AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("StartupBytes", memoryStartupBytes)).ConfigureAwait(false);

            }
        }

        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Disks(TypedPsObject<VirtualMachineInfo> vmInfo,
            Option<Seq<VirtualMachineDiskConfig>> diskConfig, VirtualMachineConfig vmConfig, IPowershellEngine engine, Func<string, Task> reportProgress)
        {
            return await diskConfig.MatchAsync(
                None: () => vmInfo,
                Some: async config =>
                {
                    var res = (await config.Map(disk => Disk(disk, engine, vmInfo, vmConfig, reportProgress))
                        .Traverse(x => x).ConfigureAwait(false)).Traverse(x => x);

                    return res.Map(infoList=> infoList.Last());

                }).ConfigureAwait(false);
        }

        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Disk(
            VirtualMachineDiskConfig diskConfig,
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            VirtualMachineConfig vmConfig,
            Func<string,Task> reportProgress)
        {
            var vhdPath = GetVhdPath(diskConfig, vmConfig);

            if (!File.Exists(vhdPath))
            {
                await reportProgress($"Create VHD: {diskConfig.Name}").ConfigureAwait(false);

                await engine.RunAsync(PsCommandBuilder.Create().Script(
                        $"New-VHD -Path \"{vhdPath}\" -ParentPath \"{diskConfig.Template}\" -Differencing"),
                    reportProgress: p => ReportPowershellProgress($"Create VHD {diskConfig.Name}", p, reportProgress)).ConfigureAwait(false);
            }

            await GetOrCreateInfoAsync(vmInfo,
                i => i.HardDrives,
                disk => vhdPath.Equals(disk.Path, StringComparison.OrdinalIgnoreCase),
                async () =>
                {
                    await reportProgress($"Add VHD: {diskConfig.Name}").ConfigureAwait(false);
                    return (await engine.GetObjectsAsync<HardDiskDriveInfo>(PsCommandBuilder.Create()
                        .AddCommand("Add-VMHardDiskDrive")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("Path", vhdPath)).ConfigureAwait(false));

                }).ConfigureAwait(false);

            return vmInfo.Recreate();
        }

        public static async Task Network(
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            VirtualMachineNetworkConfig networkConfig,
            VirtualMachineConfig vmConfig,
            Func<string, Task> reportProgress)
        {

            var optionalAdapter = await GetOrCreateInfoAsync(vmInfo,
                i => i.NetworkAdapters,
                adapter => networkConfig.Name.Equals(adapter.Name, StringComparison.OrdinalIgnoreCase),
                async () =>
                {
                    await reportProgress($"Add Network Adapter: {networkConfig.Name}").ConfigureAwait(false);
                    return await engine.GetObjectsAsync<VMNetworkAdapter>(PsCommandBuilder.Create()
                        .AddCommand("Add-VmNetworkAdapter")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("Name", networkConfig.Name)
                        .AddParameter("SwitchName", networkConfig.SwitchName)).ConfigureAwait(false);

                }).ConfigureAwait(false);

            //optionalAdapter.Map(async (adapter) =>
            //{
            //    if (!adapter.Value.Connected || adapter.Value.SwitchName != networkConfig.SwitchName)
            //    {
            //        await reportProgress($"Connect Network Adapter {adapter.Value.Name} to switch {networkConfig.SwitchName}").ConfigureAwait(false);
            //        await engine.RunAsync(
            //            PsCommandBuilder.Create().AddCommand("Connect-VmNetworkAdapter")
            //                .AddParameter("VMNetworkAdapter", adapter.PsObject)
            //                .AddParameter("SwitchName", networkConfig.SwitchName)).ConfigureAwait(false);

            //    }
            //});


        }

        private static async Task<Either<PowershellFailure, TypedPsObject<TSub>>> GetOrCreateInfoAsync<T, TSub>(TypedPsObject<T> parentInfo,
            Expression<Func<T, IList<TSub>>> listProperty,
            Func<TSub, bool> predicateFunc,
            Func<Task<Either<PowershellFailure, Seq<TypedPsObject<TSub>>>>> creatorFunc)
        {
            var result = parentInfo.GetList(listProperty, predicateFunc).ToArray();

            if (result.Length() != 0)
                return Try(result.Single()).Try().Match<Either<PowershellFailure, TypedPsObject<TSub>>>(
                    Fail: ex => Left(new PowershellFailure {Message = ex.Message}),
                    Succ: x => Right(x)
                );


            var creatorResult = await creatorFunc().ConfigureAwait(false);
            var res = creatorResult.Bind(
                seq => seq.HeadOrNone().ToEither(() =>
                    new PowershellFailure {Message = "Object creation was succesful, but no result was returned."}));

            return res;
        }


        private static string GetVhdPath(
            Option<VirtualMachineDiskConfig> optionalDiskConfig,
            Option<VirtualMachineConfig> optionalVmConfig) => (

            from diskConfig in optionalDiskConfig
            from vmConfig in optionalVmConfig
            select fun(() =>
            {
                var vhdPathRoot = diskConfig.Path;
                if (string.IsNullOrWhiteSpace(vhdPathRoot))
                    vhdPathRoot = Path.Combine(vmConfig.Path, $"{vmConfig.Name}\\Virtual Hard Disks");

                return Path.Combine(vhdPathRoot, $"{diskConfig.Name}.vhdx");
            })()).ValueUnsafe();


        //private static void ConvergeDefinition(
        //    IPowershellEngine engine, 
        //    ref TypedPsObject<VirtualMachineInfo> vmInfo)
        //{
        //    if(vmInfo == null)
        //        vmInfo = CreateVirtualMachine(engine,)
        //}


        public static Task<Either<PowershellFailure,TypedPsObject<VirtualMachineInfo>>> CreateVirtualMachine(
            IPowershellEngine engine,
            string vmName,
            string vmPath,
            int startupMemory)
        {
            var memoryStartupBytes = startupMemory * 1024L * 1024;


            return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                        .AddCommand("New-VM")
                        .AddParameter("Name", vmName)
                        .AddParameter("Path", vmPath)
                        .AddParameter("MemoryStartupBytes", memoryStartupBytes)
                        .AddParameter("Generation", 2))          
                .MapAsync(x => x.Head).MapAsync(
                result =>
                {         
                    engine.RunAsync(PsCommandBuilder.Create().AddCommand("Get-VMNetworkAdapter")
                            .AddParameter("VM", result.PsObject).AddCommand("Remove-VMNetworkAdapter"));

                    return result;
                });

        }

        private static async Task ReportPowershellProgress(string taskName, int progress, Func<string, Task> progressMessage)
        {
            await progressMessage($"{taskName}: {progress} % completed").ConfigureAwait(false);
        }


        private static Either<PowershellFailure, Unit> CreateConfigDriveDirectory(string configDrivePath)
        {
            if (Directory.Exists(configDrivePath)) return Unit.Default;

            var tryResult = Try(Directory.CreateDirectory(configDrivePath)).Try();

            if (tryResult.IsFaulted)
                return new PowershellFailure { Message = $"Failed to create directory {configDrivePath}" };

            return Unit.Default;
        }

        private static Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>> EjectConfigDriveDisk(
            string configDriveIsoPath,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            IPowershellEngine engine)
        {
            var res = vmInfo.GetList(l => l.DVDDrives,
                drive => configDriveIsoPath.Equals(drive.Path, StringComparison.OrdinalIgnoreCase))
                .Apply(dvdDriveList =>
            {
                var array = dvdDriveList.ToArray();

                if (array.Length > 1)
                    array.Take(array.Length - 1).Iter(r =>
                        engine.Run(PsCommandBuilder.Create()
                            .AddCommand("Remove-VMDvdDrive")
                            .AddParameter("VMDvdDrive", r.PsObject)));

                return vmInfo.GetList(l => l.DVDDrives, drive => configDriveIsoPath.Equals(drive.Path, StringComparison.OrdinalIgnoreCase));
            }).Apply(driveInfos =>
                {
                    return driveInfos.Map(driveInfo => engine.Run(PsCommandBuilder.Create()
                        .AddCommand("Set-VMDvdDrive")
                        .AddParameter("VMDvdDrive", driveInfo.PsObject)
                        .AddParameter("Path", null)))
                        .Traverse(x => x).Map(
                            x => vmInfo.Recreate());
                });

            return res;
        }

        private static Task<Either<PowershellFailure, Unit>> InsertConfigDriveDisk(
            string configDriveIsoPath,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            IPowershellEngine engine)
        {
            return vmInfo.GetList(l => l.DVDDrives, drive => string.IsNullOrWhiteSpace(drive.Path))
                .HeadOrNone().MatchAsync(
                    None: () => engine.RunAsync(
                        PsCommandBuilder.Create().AddCommand("Add-VMDvdDrive")
                            .AddParameter("VM", vmInfo.PsObject).AddParameter("Path", configDriveIsoPath)),
                    Some: dvdDriveInfo => engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMDvdDrive")
                        .AddParameter("VMDvdDrive", dvdDriveInfo.PsObject)
                        .AddParameter("Path", configDriveIsoPath)
                    ));
        }


        private static Either<PowershellFailure, Unit> GenerateConfigDriveDisk(
            string configDriveIsoPath,
            string hostname,
            JObject userdata)
        {
            try
            {
                GeneratorBuilder.Init()
                    .NoCloud(new NoCloudConfigDriveMetaData(hostname))
                    .SwapFile()
                    .UserData(userdata)
                    .Processing()
                    .Image().ImageFile(configDriveIsoPath)
                    .Generate();

            }
            catch (Exception ex)
            {
                return new PowershellFailure {Message = ex.Message};
            }

            return Unit.Default;
        }

        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> CloudInit(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            string vmConfigPath,
            string hostname,
            JObject userdata, 
            IPowershellEngine engine,
            Func<string, Task<Unit>> reportProgress
            )
        {

            var configDrivePath = Path.Combine(vmConfigPath, $"{vmInfo.Value.Name}\\Config");
            var configDriveIsoPath = Path.Combine(configDrivePath, "configdrive.iso");

            await reportProgress("Updating configdrive disk").ConfigureAwait(false);

            var res = await CreateConfigDriveDirectory(configDrivePath)
                .Bind(_ => EjectConfigDriveDisk(configDriveIsoPath, vmInfo, engine))
                .ToAsync()  // could be better => but I don't know how to pass info without nesting
                
                .IfRightAsync(
                    info =>
                    {
                        GenerateConfigDriveDisk(configDriveIsoPath, hostname, userdata);
                        return InsertConfigDriveDisk(configDriveIsoPath, info, engine);
                    }).ConfigureAwait(false);

            return res.Return(vmInfo);
        }
    }
}