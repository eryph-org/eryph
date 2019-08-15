using System;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class ConvergeTaskRequestedEventHandler : IHandleMessages<AcceptedOperation<ConvergeVirtualMachineCommand>>
    {
        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;
        private Guid _operationId;

        public ConvergeTaskRequestedEventHandler(
            IPowershellEngine engine,
            IBus bus)
        {
            _engine = engine;
            _bus = bus;
        }

        public Task Handle(AcceptedOperation<ConvergeVirtualMachineCommand> message)
        {
            var command = message.Command;
            var config = command.Config;
            _operationId = command.OperationId;

            var chain = 
                from normalizedVMConfig in Converge.NormalizeMachineConfig(config, _engine, ProgressMessage)
                from vmList in GetVmInfo(normalizedVMConfig.Id, _engine)
                from optionalVmInfo in EnsureUnique(vmList, normalizedVMConfig.Id)
                from storageSettings in DetectStorageSettings(optionalVmInfo)
                from vmConfig in ApplyStorageSettings(normalizedVMConfig, storageSettings,GetHostSettings())
                from vmInfoCreated in EnsureCreated(optionalVmInfo, vmConfig, storageSettings, _engine)
                from vmInfo in EnsureNameConsistent(vmInfoCreated, vmConfig, _engine)
                from _ in AttachToOperation(vmInfo, _bus, command.OperationId)
                from vmInfoConverged in ConvergeVm(vmInfo, vmConfig, storageSettings, _engine)
                select vmInfoConverged;

            return chain.ToAsync().MatchAsync(
                LeftAsync: HandleError,
                RightAsync: vmInfo2 => _bus.Send(new OperationCompletedEvent
                {
                    OperationId = command.OperationId,
                }).ToUnit());

        }

        private async Task<Unit> ProgressMessage(string message)
        {
            using (var scope = new RebusTransactionScope())
            {
                await _bus.Send(new ConvergeVirtualMachineProgressEvent
                {
                    OperationId = _operationId,
                    Message = message
                }).ConfigureAwait(false);

                // commit it like this
                await scope.CompleteAsync().ConfigureAwait(false);
            }
            return Unit.Default;

        }

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> ConvergeVm(TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig machineConfig, VMStorageSettings storageSettings, IPowershellEngine engine)
        {
            return
                from infoFirmware in Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                from infoCpu in Converge.Cpu(infoFirmware, machineConfig.VM.Cpu, engine, ProgressMessage)
                from infoDisks in Converge.Disks(infoCpu, machineConfig.VM.Disks.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoNetworks in Converge.NetworkAdapters(infoDisks, machineConfig.VM.NetworkAdapters.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoCloudInit in Converge.CloudInit(infoNetworks, machineConfig.VM.Path, storageSettings.StorageIdentifier, machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)
                select infoCloudInit;
                
                //Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                //.BindAsync(info => Converge.Cpu(info, machineConfig.VMConfig.Cpu, engine, ProgressMessage))
                //.BindAsync(info => Converge.Disks(info, machineConfig.VMConfig.Disks?.ToSeq(), machineConfig, engine, ProgressMessage))

                //.BindAsync(info => Converge.CloudInit(
                //    info, machineConfig.VMConfig.Path, machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)).ConfigureAwait(false);

            //await Converge.Definition(engine, vmInfo, config, ProgressMessage).ConfigureAwait(false);
            //await ProgressMessage("Generate Virtual Machine provisioning disk").ConfigureAwait(false);

        }

        private async Task<Either<PowershellFailure, Option<TypedPsObject<VirtualMachineInfo>>>> EnsureUnique(Seq<TypedPsObject<VirtualMachineInfo>> list, string id)
        {
            if (list.Count > 1)
                return Prelude.Left(new PowershellFailure { Message = $"VM id '{id}' is not unique." });

            return Prelude.Right(list.HeadOrNone());
        }


        private Task<Either<PowershellFailure, VMStorageSettings>> DetectStorageSettings(Option<TypedPsObject<VirtualMachineInfo>> optionalVmInfo)
        {
            return Prelude.Try(() => optionalVmInfo.Match(
                None: () => new VMStorageSettings {StorageIdentifier = Guid.NewGuid().ToString()},
                Some: (vmInfo) =>
                {
                    var vmStorageIdentifier = ParentDirectoryName(vmInfo.Value.Path);

                    var storageIdentifier = vmStorageIdentifier.Match(
                        None: () => throw new Exception(
                            "Invalid vm path. For haipa all vms have be in a sub folder at least one level below disk root"),
                        Some:
                        s => s);

                    var storageIdentifiers = Seq<Option<string>>.Empty;
                    storageIdentifiers = storageIdentifiers.Add(ParentDirectoryName(vmInfo.Value.SmartPagingFilePath));
                    storageIdentifiers = storageIdentifiers.Add(ParentDirectoryName(vmInfo.Value.SnapshotFileLocation));

                    foreach (var optionalIdentifier in storageIdentifiers)
                    {
                        optionalIdentifier.Match(
                            Some: (identifier) =>
                            {
                                if (!storageIdentifier.Equals(identifier, StringComparison.InvariantCultureIgnoreCase))
                                    throw new Exception(
                                        $"Failed to verify storage identifier '{storageIdentifier}'. At least vm path resolves to another identifier ('{identifier}')");
                            },
                            None: () => throw new Exception(
                                $"Failed to verify storage identifier '{storageIdentifier}'. At least one path has no parent folder."));
                    }

                    return new VMStorageSettings {StorageIdentifier = storageIdentifier};


                }))().Match(
                Succ: r => r,
                Fail: ex => Prelude.Left<PowershellFailure, VMStorageSettings>(new PowershellFailure{Message = ex.Message})).ToAsync().ToEither();

        }

#pragma warning disable 1998
        private async Task<Either<PowershellFailure, MachineConfig>> ApplyStorageSettings(MachineConfig config, VMStorageSettings storageSettings, HostSettings hostSettings)
#pragma warning restore 1998
        {
            if (string.IsNullOrWhiteSpace(config.VM.Path))
            {
                config.VM.Path = hostSettings.DefaultDataPath; //storage identifier will be added automatically by ps command
            }

            foreach (var diskConfig in config.VM.Disks)
            {
                var diskRoot = hostSettings.DefaultVirtualHardDiskPath;
                diskRoot = Path.Combine(diskRoot, storageSettings.StorageIdentifier);
                diskConfig.Path = Path.Combine(diskRoot, $"{diskConfig.Name}.vhdx");
            }      

            return config;
        }

        private static Option<string> ParentDirectoryName(string path)
        {
            var directoryInfo = new DirectoryInfo(path);

            if (directoryInfo.Parent == null)
                return Prelude.None;

            return directoryInfo.Parent.Name;
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureCreated(Option<TypedPsObject<VirtualMachineInfo>> vmInfo, MachineConfig config, VMStorageSettings storageSettings, IPowershellEngine engine)
        {
            return vmInfo.MatchAsync(
                None: () => Converge.CreateVirtualMachine(engine, config.Name, storageSettings.StorageIdentifier,
                    config.VM.Path,
                    config.VM.Memory.Startup),
                Some: s => s
            );

        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureNameConsistent(TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig config, IPowershellEngine engine)
        {
            return Prelude.Cond<(string currentName, string newName)>((names) =>
                    !string.IsNullOrWhiteSpace(names.newName) &&
                    !names.newName.Equals(names.currentName, StringComparison.InvariantCulture))((vmInfo.Value.Name,
                    config.Name))

                .MatchAsync(
                    None: () =>
                    {
                        return vmInfo;
                    },
                    Some: (some) =>
                    {
                        return Converge.RenameVirtualMachine(engine, vmInfo, config.Name); 
                    });


        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> AttachToOperation(Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>> vmInfo, IBus bus, Guid operationId)
        {
            return vmInfo.MapAsync( async
                info =>
                {
                    await bus.Send(new AttachMachineToOperationCommand
                    {
                        OperationId = operationId,
                        AgentName = Environment.MachineName,
                        MachineId = info.Value.Id,
                    }).ConfigureAwait(false);
                    return info;
                }

            );

        }

        private static Task<Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(string id,
            IPowershellEngine engine)
        {
            return Prelude.Cond<string>((c) => !string.IsNullOrWhiteSpace(c))(id).MatchAsync(
                None:  () =>
                {
                    return Seq<TypedPsObject<VirtualMachineInfo>>.Empty;
                },
                Some: (s) =>
                {
                    return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                        .AddCommand("get-vm").AddParameter("Id", s)
                        //this a bit dangerous, because there may be other errors causing the 
                        //command to fail. However there seems to be no other way except parsing error response
                        .AddParameter("ErrorAction", "SilentlyContinue")
                    );
                });
        }

        private async Task<Unit> HandleError(PowershellFailure failure)
        {

            await _bus.Send(new OperationFailedEvent()
            {
                OperationId = _operationId,
                ErrorMessage = failure.Message,
            }).ConfigureAwait(false);

            return Unit.Default;
        }

        public HostSettings GetHostSettings()
        {

            var scope = new ManagementScope(@"\\.\root\virtualization\v2");
            var query = new ObjectQuery("select DefaultExternalDataRoot,DefaultVirtualHardDiskPath from Msvm_VirtualSystemManagementServiceSettingData");


            var searcher = new ManagementObjectSearcher (scope, query );
            var settingsCollection = searcher.Get ( );
            
            foreach (var hostSettings in settingsCollection)
            {
                return new HostSettings
                {
                    DefaultVirtualHardDiskPath = hostSettings.GetPropertyValue("DefaultVirtualHardDiskPath")?.ToString(),
                    DefaultDataPath = hostSettings.GetPropertyValue("DefaultExternalDataRoot")?.ToString(),
                };
            }

            throw new Exception("failed to query for hyper-v host settings");

        }

    }

    internal class VMStorageSettings
    {
        public string StorageIdentifier { get; set; }
    }
}