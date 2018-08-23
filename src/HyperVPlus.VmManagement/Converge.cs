using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Contiva.CloudInit.ConfigDrive.Generator;
using Contiva.CloudInit.ConfigDrive.NoCloud;
using Contiva.CloudInit.ConfigDrive.Processing;
using HyperVPlus.VmConfig;
using HyperVPlus.VmManagement.Data;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Newtonsoft.Json.Linq;

namespace HyperVPlus.VmManagement
{
    public static class Converge
    {

        public static async Task Definition(
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            VirtualMachineConfig vmConfig,
            Func<string, Task> reportProgress)
        {
            var changed = false;

            using (await vmInfo.AquireLockAsync().ConfigureAwait(false))
            {
                if (vmInfo.Value.Generation >= 2)
                {
                    (await engine.GetObjectAsync<VMFirmwareInfo>(PsCommandBuilder.Create()
                        .AddCommand("Get-VMFirmware")
                        .AddParameter("VM", vmInfo.PsObject)).ConfigureAwait(false)).Map(async (firmwareInfo) =>
                        {
                            if (firmwareInfo.Value.SecureBoot != OnOffState.Off)
                            {
                                await reportProgress($"Configure VM Firmware - Secure Boot: {OnOffState.Off}")
                                    .ConfigureAwait(false);

                                await engine.RunAsync(PsCommandBuilder.Create()
                                    .AddCommand("Set-VMFirmware")
                                    .AddParameter("VM", vmInfo.PsObject)
                                    .AddParameter("EnableSecureBoot", OnOffState.Off)).ConfigureAwait(false);

                                changed = true;

                            }
                        });                   
                }


                if (vmInfo.Value.ProcessorCount != vmConfig.Cpu.Count)
                {
                    await reportProgress($"Configure VM Processor: Count: {vmConfig.Cpu.Count}").ConfigureAwait(false);

                    await engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMProcessor")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("Count", vmConfig.Cpu.Count)).ConfigureAwait(false);
                    changed = true;
                }

                var memoryStartupBytes = vmConfig.Memory.Startup * 1024L * 1024;

                if (vmInfo.Value.MemoryStartup != memoryStartupBytes)
                {
                    await reportProgress($"Configure VM Memory: Startup: {vmConfig.Memory.Startup} MB").ConfigureAwait(false);

                    await engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMMemory")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("StartupBytes", memoryStartupBytes)).ConfigureAwait(false);

                    changed = true;

                }
            }

            //if(changed)
            //    vmInfo.Refresh();
        }

        public static async Task Disk(
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            VirtualMachineDiskConfig diskConfig,
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
                    return await engine.GetObjectAsync<HardDiskDriveInfo>(PsCommandBuilder.Create()
                        .AddCommand("Add-VMHardDiskDrive")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("Path", vhdPath)).ConfigureAwait(false);

                }).ConfigureAwait(false);

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
                    return await engine.GetObjectAsync<VMNetworkAdapter>(PsCommandBuilder.Create()
                        .AddCommand("Add-VmNetworkAdapter")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("Name", networkConfig.Name)
                        .AddParameter("SwitchName", networkConfig.SwitchName)).ConfigureAwait(false);

                }).ConfigureAwait(false);

            optionalAdapter.Map(async (adapter) =>
            {
                if (!adapter.Value.Connected || adapter.Value.SwitchName != networkConfig.SwitchName)
                {
                    await reportProgress($"Connect Network Adapter {adapter.Value.Name} to switch {networkConfig.SwitchName}").ConfigureAwait(false);
                    await engine.RunAsync(
                        PsCommandBuilder.Create().AddCommand("Connect-VmNetworkAdapter")
                            .AddParameter("VMNetworkAdapter", adapter.PsObject)
                            .AddParameter("SwitchName", networkConfig.SwitchName)).ConfigureAwait(false);

                }
            });


        }

        private static async Task<Either<PowershellFailure, TypedPsObject<TSub>>> GetOrCreateInfoAsync<T, TSub>(TypedPsObject<T> parentInfo,
            Expression<Func<T, IList<TSub>>> listProperty,
            Func<TSub, bool> predicateFunc,
            Func<Task<Either<PowershellFailure, TypedPsObject<TSub>>>> creatorFunc)
        {
            using (await parentInfo.AquireLockAsync().ConfigureAwait(false))
            {
                return parentInfo.GetList(listProperty, predicateFunc).SingleOrDefault() ?? (await creatorFunc().ConfigureAwait(false));
            }
        }


        private static string GetVhdPath(
            Option<VirtualMachineDiskConfig> optionalDiskConfig,
            Option<VirtualMachineConfig> optionalVmConfig) => (

            from diskConfig in optionalDiskConfig
            from vmConfig in optionalVmConfig
            select Prelude.fun(() =>
            {
                var vhdPathRoot = diskConfig.Path;
                if (String.IsNullOrWhiteSpace(vhdPathRoot))
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


        public static async Task<Either<PowershellFailure,TypedPsObject<VirtualMachineInfo>>> CreateVirtualMachine(
            IPowershellEngine engine,
            string vmName,
            string vmPath,
            int startupMemory)
        {
            var memoryStartupBytes = startupMemory * 1024L * 1024;


            var vmInfo = await (await engine
                    .GetObjectAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                        .AddCommand("New-VM")
                        .AddParameter("Name", vmName)
                        .AddParameter("Path", vmPath)
                        .AddParameter("MemoryStartupBytes", memoryStartupBytes)
                        .AddParameter("Generation", 2)).ConfigureAwait(false))
                .MapAsync(async (r) =>
                {
                    await engine.RunAsync(PsCommandBuilder.Create().AddCommand("Get-VMNetworkAdapter")
                        .AddParameter("VM", r.PsObject).AddCommand("Remove-VMNetworkAdapter")).ConfigureAwait(false);
                    //r.Refresh();
                    return r;
                }).ConfigureAwait(false);
               
            return vmInfo;
        }

        private static async Task ReportPowershellProgress(string taskName, int progress, Func<string, Task> progressMessage)
        {
            await progressMessage($"{taskName}: {progress} % completed").ConfigureAwait(false);
        }

        public static async Task CloudInit(IPowershellEngine engine, 
            string vmConfigPath, 
            string hostname, 
            JObject userdata, 
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            var configDrivePath = Path.Combine(vmConfigPath, $"{vmInfo.Value.Name}\\Config");
            if (!Directory.Exists(configDrivePath))
                Directory.CreateDirectory(configDrivePath);

            configDrivePath = Path.Combine(configDrivePath, "configdrive.iso");

            using (await vmInfo.AquireLockAsync().ConfigureAwait(false))
            {
                var dvdDriveList = vmInfo.GetList(l => l.DVDDrives,
                    drive => configDrivePath.Equals(drive.Path, StringComparison.OrdinalIgnoreCase)).ToArray();

                if (dvdDriveList.Length > 1)
                    dvdDriveList.Take(dvdDriveList.Length - 1).Iter(r =>
                        engine.Run(PsCommandBuilder.Create()
                            .AddCommand("Remove-VMDvdDrive")
                            .AddParameter("VMDvdDrive", r.PsObject)));

                var dvdDriveInfo = vmInfo.GetList(l => l.DVDDrives,
                    drive => configDrivePath.Equals(drive.Path, StringComparison.OrdinalIgnoreCase)).SingleOrDefault();

                if (dvdDriveInfo != null)
                {
                    await engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMDvdDrive")
                        .AddParameter("VMDvdDrive", dvdDriveInfo.PsObject)
                        .AddParameter("Path", null)).ConfigureAwait(false);
                }
            }

            GeneratorBuilder.Init()
                .NoCloud(new NoCloudConfigDriveMetaData(hostname))
                .SwapFile()
                .UserData(userdata)
                .Processing()
                .ImageFile(configDrivePath)
                .Generate();


            using (await vmInfo.AquireLockAsync().ConfigureAwait(false))
            {
                //vmInfo.Refresh();

                var dvdDriveInfo = vmInfo.GetList(l => l.DVDDrives,
                    drive => string.IsNullOrWhiteSpace(drive.Path)).FirstOrDefault();

                if (dvdDriveInfo != null)
                {
                    await engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMDvdDrive")
                        .AddParameter("VMDvdDrive", dvdDriveInfo.PsObject)
                        .AddParameter("Path", configDrivePath)).ConfigureAwait(false);
                }
                else
                {
                    await engine.RunAsync(
                        PsCommandBuilder.Create().AddCommand("Add-VMDvdDrive")
                            .AddParameter("VM", vmInfo.PsObject).AddParameter("Path", configDrivePath)).ConfigureAwait(false);
                }

            }
        }
    }
}