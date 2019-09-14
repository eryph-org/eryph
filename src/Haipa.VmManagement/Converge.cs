using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Haipa.CloudInit.ConfigDrive.Generator;
using Haipa.CloudInit.ConfigDrive.NoCloud;
using Haipa.CloudInit.ConfigDrive.Processing;
using Haipa.Modules.VmHostAgent;
using Haipa.VmConfig;
using Haipa.VmManagement.Data;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Newtonsoft.Json.Linq;
using static LanguageExt.Prelude;

namespace Haipa.VmManagement
{
    public static class Converge
    {
#pragma warning disable 1998
        public static async Task<Either<PowershellFailure, MachineConfig>> NormalizeMachineConfig(
#pragma warning restore 1998
            MachineConfig config,  IPowershellEngine engine, Func<string, Task> reportProgress)
        {
            var machineConfig = config;

            if (machineConfig.VM== null)
                machineConfig.VM = new VirtualMachineConfig();

            if (string.IsNullOrWhiteSpace(machineConfig.Name) && string.IsNullOrWhiteSpace(machineConfig.Id))
            {
                //TODO generate a random name here
                machineConfig.Name = "haipa-machine";
            }


            if (machineConfig.VM.Cpu == null)
                machineConfig.VM.Cpu = new VirtualMachineCpuConfig {Count = 1};

            if (machineConfig.VM.Memory == null)
                machineConfig.VM.Memory = new VirtualMachineMemoryConfig() { Startup = 1024 };

            if (machineConfig.VM.Disks == null)
                machineConfig.VM.Disks = new List<VirtualMachineDiskConfig>();

            if (machineConfig.VM.NetworkAdapters == null)
                machineConfig.VM.NetworkAdapters = new List<VirtualMachineNetworkAdapterConfig>();

            if (machineConfig.Provisioning == null)
                machineConfig.Provisioning = new VirtualMachineProvisioningConfig();

            if (string.IsNullOrWhiteSpace(machineConfig.Provisioning.Hostname))
                machineConfig.Provisioning.Hostname = machineConfig.Name;

            foreach (var adapterConfig in machineConfig.VM.NetworkAdapters)
            {
                if (adapterConfig.MacAddress != null)
                {
                    adapterConfig.MacAddress = adapterConfig.MacAddress.Replace("-", "");
                    adapterConfig.MacAddress = adapterConfig.MacAddress.Replace(":", "");
                    adapterConfig.MacAddress = adapterConfig.MacAddress.ToLowerInvariant();
                }
                else
                {
                    adapterConfig.MacAddress = "";
                }
            }

            return machineConfig;
        }


        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Firmware(TypedPsObject<VirtualMachineInfo> vmInfo,
            MachineConfig config, IPowershellEngine engine, Func<string, Task> reportProgress)
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
            MachineConfig vmConfig,
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


            if (vmInfo.Value.ProcessorCount != vmConfig.VM.Cpu.Count)
            {
                await reportProgress($"Configure VM Processor: Count: {vmConfig.VM.Cpu.Count}").ConfigureAwait(false);

                await engine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Set-VMProcessor")
                    .AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Count", vmConfig.VM.Cpu.Count)).ConfigureAwait(false);
            }

            var memoryStartupBytes = vmConfig.VM.Memory.Startup * 1024L * 1024;

            if (vmInfo.Value.MemoryStartup != memoryStartupBytes)
            {
                await reportProgress($"Configure VM Memory: Startup: {vmConfig.VM.Memory.Startup} MB").ConfigureAwait(false);

                await engine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Set-VMMemory")
                    .AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("StartupBytes", memoryStartupBytes)).ConfigureAwait(false);

            }
        }

        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Disks(TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VMDiskStorageSettings> plannedDiskStorageSettings, HostSettings hostSettings, IPowershellEngine engine, Func<string, Task> reportProgress)
        {
            var currentCheckpointType = vmInfo.Value.CheckpointType;
            try
            {
                return await (
                    from _ in SetVMCheckpointType(vmInfo, CheckpointType.Disabled, engine).ToAsync()
                    from currentDiskSettingsList in Storage.DetectDiskStorageSettings(vmInfo.Value.HardDrives,
                        hostSettings, engine).ToAsync()
                    from vmInfoAfterDetach in DetachUndefinedDisks(engine, vmInfo, plannedDiskStorageSettings,
                        currentDiskSettingsList, reportProgress).ToAsync()
                    let convergeDisk = fun(((VMDiskStorageSettings e) =>
                        Disk(e, engine, vmInfoAfterDetach, currentDiskSettingsList, reportProgress)))
                    from res in plannedDiskStorageSettings.MapToEitherAsync(convergeDisk).LastOrNone().ToAsync()
                    select res.IfNone(vmInfo)).ToEither().ConfigureAwait(false);
            }
            finally
            {
                await SetVMCheckpointType(vmInfo, currentCheckpointType, engine).ConfigureAwait(false);
            }


        }


        public static Task<Either<PowershellFailure, Unit>> SetVMCheckpointType(TypedPsObject<VirtualMachineInfo> vmInfo, CheckpointType checkpointType, IPowershellEngine engine)
        {
            return engine.RunAsync(new PsCommandBuilder()
                .AddCommand("Set-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("CheckpointType", checkpointType));

        }

        //private static Task<Either<PowershellFailure, Seq<VMDiskStorageSettings>>> ToStorageSettings(
        //    this Seq<VirtualMachineDiskConfig> diskConfig, VMStorageSettings storageSettings, TypedPsObject<VirtualMachineInfo> vmInfo)
        //{
        //    return Try(
        //            from dc in diskConfig
        //            let existingDisk = FindVMDiskByName(vmInfo, dc)
        //            select dc.ToDiskStorageSettings(existingDisk, storageSettings)
        //            )
        //        .ToEither(ex => new PowershellFailure {Message = ex.Message})
        //        .ToAsync().ToEither();

        //}

        //private static Option<HardDiskDriveInfo> FindVMDiskByName(this TypedPsObject<VirtualMachineInfo> vmInfo, VirtualMachineDiskConfig config)
        //{
        //    return vmInfo.Value.HardDrives.Find(d => Path.GetFileNameWithoutExtension(d.Path) == config.Name);
        //}

        //private static VMDiskStorageSettings ToDiskStorageSettings(this VirtualMachineDiskConfig diskConfig, Option<HardDiskDriveInfo> optionalInfo, VMStorageSettings storageSettings)
        //{
        //    return optionalInfo.Match(
        //        None: () => new VMDiskStorageSettings
        //        {
        //            Name = diskConfig.Name,
        //            Path = Path.Combine(
        //                Path.Combine(storageSettings.VMPath, storageSettings.StorageIdentifier.ValueUnsafe()),
        //                $"{diskConfig.Name}.vhdx"),
        //            ParentPath = diskConfig.Template
        //        },
        //        Some: (diskInfo) => new VMDiskStorageSettings { Name = diskConfig.Name, Path = diskInfo.Path});
        //}

        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> DetachUndefinedDisks(IPowershellEngine engine, TypedPsObject<VirtualMachineInfo> vmInfo, Seq<VMDiskStorageSettings> plannedStorageSettings, Seq<CurrentVMDiskStorageSettings> currentStorageSettings,  Func<string, Task> reportProgress)
        {
            var diskPaths = plannedStorageSettings.Map(x => x.AttachPath).MapT(y => y.ToLowerInvariant()).Somes();
            var frozenDiskIds = currentStorageSettings.Where(x => x.Frozen).Map(x=>x.AttachedVMId);

            return FindAndApply(vmInfo,
                    i => i.HardDrives,
                    hd =>
                    {

                        var path = hd.Path?.ToLowerInvariant();
                        var detach = !diskPaths.Contains(path) && !frozenDiskIds.Contains(hd.Id);
                        return detach;
                    },
                    i => engine.RunAsync(PsCommandBuilder.Create().AddCommand("Remove-VMHardDiskDrive")
                        .AddParameter("VMHardDiskDrive", i.PsObject))).Map(x => x.Lefts().HeadOrNone())
                .MatchAsync(
                    Some: l => LeftAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(l).ToEither(),
                    None: () => RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo.Recreate())
                        .ToEither());

        }

        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Disk(    
                VMDiskStorageSettings diskSettings,
                IPowershellEngine engine,
                TypedPsObject<VirtualMachineInfo> vmInfo,
                Seq<CurrentVMDiskStorageSettings> currentStorageSettings,
                Func<string,Task> reportProgress)
        {

            var currentSettings =
                currentStorageSettings.Find(x => diskSettings.Path.Equals(x.Path, StringComparison.OrdinalIgnoreCase) 
                && diskSettings.Name.Equals(x.Name, StringComparison.InvariantCultureIgnoreCase) );
            var frozenOptional = currentSettings.Map(x => x.Frozen);

            if (frozenOptional.IsSome && frozenOptional.ValueUnsafe())
            {
                await reportProgress($"Skipping disk '{diskSettings.Name}': storage management is disabled for this disk.").ConfigureAwait(false);
                return vmInfo;
            }



            return await diskSettings.AttachPath.Map(async (vhdPath) =>
            {

                if (!File.Exists(vhdPath))
                {
                    await reportProgress($"Create VHD: {diskSettings.Name}").ConfigureAwait(false);

                    var createDiskResult = await diskSettings.ParentPath.Match(Some: parentPath =>
                        {
                            return engine.RunAsync(PsCommandBuilder.Create().Script(
                                $"New-VHD -Path \"{vhdPath}\" -ParentPath \"{parentPath}\" -Differencing"));
                        },
                        None: () =>
                        {
                            return engine.RunAsync(PsCommandBuilder.Create().Script(
                                $"New-VHD -Path \"{vhdPath}\" -Dynamic -SizeBytes {diskSettings.SizeBytes}"));
                        });

                    if (createDiskResult.IsLeft)
                        return Prelude.Left(createDiskResult.LeftAsEnumerable().FirstOrDefault());
                }

                var sizeResult = await engine
                    .GetObjectsAsync<VhdInfo>(new PsCommandBuilder().AddCommand("get-vhd").AddArgument(vhdPath))
                    .BindAsync(x => x.HeadOrLeft(new PowershellFailure())).BindAsync(async (vhdInfo) =>
                    {
                        if (vhdInfo.Value.Size != diskSettings.SizeBytes && diskSettings.SizeBytes > 0)
                        {
                            await reportProgress(
                                $"Resizing disk {diskSettings.Name} to {diskSettings.SizeBytes} bytes");
                            return await engine.RunAsync(PsCommandBuilder.Create().AddCommand("Resize-VHD")
                                .AddArgument(vhdPath)
                                .AddParameter("Size", diskSettings.SizeBytes));

                        }

                        return Unit.Default;
                    });

                if (sizeResult.IsLeft)
                    return Prelude.Left(sizeResult.LeftAsEnumerable().FirstOrDefault());


                //use a local copy of VMInfo, as in parallel other disk updates may change vmInfo
                var localVMInfo = vmInfo.Recreate();

                await GetOrCreateInfoAsync(localVMInfo,
                    i => i.HardDrives,
                    disk => currentSettings.Map(x=>x.AttachedVMId) == disk.Id,
                    async () =>
                    {
                        await reportProgress($"Add VHD: {diskSettings.Name}").ConfigureAwait(false);
                        return (await engine.GetObjectsAsync<HardDiskDriveInfo>(PsCommandBuilder.Create()
                            .AddCommand("Add-VMHardDiskDrive")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("Path", vhdPath)).ConfigureAwait(false));

                    }).ConfigureAwait(false);

                return Prelude.Right<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(localVMInfo.Recreate());
            }).IfNone(Prelude.RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo.Recreate()).ToEither);

        }


        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> NetworkAdapters(TypedPsObject<VirtualMachineInfo> vmInfo,
            Seq<VirtualMachineNetworkAdapterConfig> adapterConfig, MachineConfig vmConfig, IPowershellEngine engine, Func<string, Task> reportProgress)
        {
            var seq = adapterConfig.Map(adapter => NetworkAdapter(adapter, engine, vmInfo, vmConfig, reportProgress));

            if (seq.IsEmpty)
                return vmInfo;

            return await seq.Last;
        }

        public static async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> NetworkAdapter(
            VirtualMachineNetworkAdapterConfig networkAdapterConfig,
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            MachineConfig machineConfig,
            Func<string, Task> reportProgress)
        {

            var optionalAdapter = await GetOrCreateInfoAsync(vmInfo,
                i => i.NetworkAdapters,
                adapter => networkAdapterConfig.Name.Equals(adapter.Name, StringComparison.OrdinalIgnoreCase),
                async () =>
                {
                    await reportProgress($"Add Network Adapter: {networkAdapterConfig.Name}").ConfigureAwait(false);
                    return await engine.GetObjectsAsync<VMNetworkAdapter>(PsCommandBuilder.Create()
                        .AddCommand("Add-VmNetworkAdapter")
                        .AddParameter("Passthru")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("Name", networkAdapterConfig.Name)
                        .AddParameter("StaticMacAddress", UseOrGenerateMacAddress(networkAdapterConfig, vmInfo))
                        .AddParameter("SwitchName", networkAdapterConfig.SwitchName)).ConfigureAwait(false);

                }).ConfigureAwait(false);

                return optionalAdapter.Map(_ => vmInfo.Recreate());

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
            return vmInfo.Recreate();


        }

        private static string UseOrGenerateMacAddress(VirtualMachineNetworkAdapterConfig adapterConfig, TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var result = adapterConfig.MacAddress;
            if (string.IsNullOrWhiteSpace(result))
                result = GenerateMacAddress(vmInfo.Value.Id, adapterConfig.Name);
            return result;
        }

        private static string GenerateMacAddress(Guid valueId, string adapterName)
        {
            var id = $"{valueId}_{adapterName}";
            var crc = new Crc32();

            string result = null;

            var arrayData = Encoding.ASCII.GetBytes(id);
            var arrayResult = crc.ComputeHash(arrayData);
            foreach (var t in arrayResult)
            {
                var temp = Convert.ToString(t, 16);
                if (temp.Length == 1)
                    temp = $"0{temp}";
                result += temp;
            }
            return "d2ab" + result;
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
                    new PowershellFailure {Message = "Object creation was successful, but no result was returned."}));

            return res;
        }

        public static Task<Either<PowershellFailure,TypedPsObject<VirtualMachineInfo>>> CreateVirtualMachine(
            IPowershellEngine engine,
            string vmName,
            string storageIdentifier,
            string vmPath,
            int startupMemory)
        {
            var memoryStartupBytes = startupMemory * 1024L * 1024;


            return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("New-VM")
                    .AddParameter("Name", storageIdentifier)
                    .AddParameter("Path", vmPath)
                    .AddParameter("MemoryStartupBytes", memoryStartupBytes)
                    .AddParameter("Generation", 2))
                .MapAsync(x => x.Head).MapAsync(
                    result =>
                    {

                        engine.RunAsync(PsCommandBuilder.Create().AddCommand("Get-VMNetworkAdapter")
                            .AddParameter("VM", result.PsObject).AddCommand("Remove-VMNetworkAdapter"));

                        return result;
                    })
                .BindAsync(info => RenameVirtualMachine(engine, info, vmName));

        }

        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> RenameVirtualMachine(
            IPowershellEngine engine,
            TypedPsObject<VirtualMachineInfo> vmInfo,
            string newName)
        {
            return engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Rename-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("NewName", newName)
            ).MapAsync(u => vmInfo.Recreate());

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


        private static void GenerateConfigDriveDisk(string configDriveIsoPath,
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
                return;
            }

            return;
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
            
            var configDriveIsoPath = Path.Combine(vmConfigPath, "configdrive.iso");

            await reportProgress("Updating configdrive disk").ConfigureAwait(false);

            var res = await CreateConfigDriveDirectory(vmConfigPath)
                .Bind(_ => EjectConfigDriveDisk(configDriveIsoPath, vmInfo, engine))
                .ToAsync()              
                .IfRightAsync(
                    info =>
                    {
                        GenerateConfigDriveDisk(configDriveIsoPath, hostname, userdata);
                        return InsertConfigDriveDisk(configDriveIsoPath, info, engine);
                    }).ConfigureAwait(false);

            return res.Return(vmInfo);
        }

        private static Task<IEnumerable<TRes>> FindAndApply<T, TSub, TRes>(TypedPsObject<T> parentInfo,
    Expression<Func<T, IList<TSub>>> listProperty,
    Func<TSub, bool> predicateFunc,
    Func<TypedPsObject<TSub>, Task<TRes>> applyFunc)
        {
            return parentInfo.GetList(listProperty, predicateFunc).ToArray().Map(applyFunc)
                .Traverse(l => l);
        }
    }

    public class StorageNames
    {
        public Option<string> DataStoreName { get; set; }
        public Option<string> ProjectName { get; set; }
        public Option<string> EnvironmentName { get; set; }

    }

    public class VMStorageSettings
    {
        public StorageNames StorageNames { get; set; }
        public Option<string> StorageIdentifier { get; set; }

        public string VMPath { get; set; }
        public bool Frozen { get; set; }
    }

    public class VMDiskStorageSettings
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public Option<string> ParentPath { get; set; }
        public Option<string> AttachPath { get; set; }

        public Option<string> StorageIdentifier { get; set; }
        public StorageNames StorageNames { get; set; }
        public long SizeBytes { get; set; }
    }

    public class CurrentVMDiskStorageSettings : VMDiskStorageSettings
    {
        public bool Frozen { get; set; }
        public string AttachedVMId { get; set; }
    }

}