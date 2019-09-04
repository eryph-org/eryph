using System;
using System.Management;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using JetBrains.Annotations;
using LanguageExt;
using Rebus;
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
            var hostSettings = GetHostSettings();

            var chain = 

                from normalizedVMConfig in Converge.NormalizeMachineConfig(config, _engine, ProgressMessage)
                from vmList in GetVmInfo(normalizedVMConfig.Id, _engine)
                from optionalVmInfo in EnsureUnique(vmList, normalizedVMConfig.Id)
                                
                from currentStorageSettings in Storage.DetectVMStorageSettings(optionalVmInfo, hostSettings, ProgressMessage)
                from plannedStorageSettings in Storage.PlanVMStorageSettings(normalizedVMConfig, currentStorageSettings, hostSettings, GenerateId)
                
                from vmInfoCreated in EnsureCreated(optionalVmInfo, config, plannedStorageSettings, _engine)
                from _ in AttachToOperation(vmInfoCreated, _bus, command.OperationId)
                from vmInfo in EnsureNameConsistent(vmInfoCreated, config, _engine)

                from currentDiskStorageSettings in Storage.DetectDiskStorageSettings(vmInfo.Value.HardDrives, hostSettings, _engine)
                from plannedDiskStorageSettings in Storage.PlanDiskStorageSettings(config, plannedStorageSettings, currentDiskStorageSettings, hostSettings)

                from vmInfoConverged in ConvergeVm(vmInfo, config, plannedStorageSettings, plannedDiskStorageSettings, _engine)
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

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> ConvergeVm(
            TypedPsObject<VirtualMachineInfo> vmInfo, 
            MachineConfig machineConfig, 
            VMStorageSettings storageSettings, 
            Seq<VMDiskStorageSettings> diskStorageSettings, 
            IPowershellEngine engine)
        {
            return
                from infoFirmware in Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                from infoCpu in Converge.Cpu(infoFirmware, machineConfig.VM.Cpu, engine, ProgressMessage)
                from infoDisks in Converge.Disks(infoCpu, diskStorageSettings, engine, ProgressMessage)
                from infoNetworks in Converge.NetworkAdapters(infoDisks, machineConfig.VM.NetworkAdapters.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoCloudInit in Converge.CloudInit(infoNetworks, storageSettings.VMPath,machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)
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

        private Task<Either<PowershellFailure, string>> GenerateId()
        {
            return Prelude.TryAsync(() =>
                _bus.SendRequest<GenerateIdReply>(new GenerateIdCommand(), null, TimeSpan.FromMinutes(5))
                    .Map(r => LongToString(r.GeneratedId, 36)))
                
                .ToEither(ex => new PowershellFailure{Message = ex.Message});

        }

        public const string DefaultDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static string LongToString(BigInteger subject, int @base, string digits = DefaultDigits)
        {
            if (@base < 2) { throw (new ArgumentException("Base must not be less than 2", nameof(@base))); }
            if (digits.Length < @base) { throw (new ArgumentException("Not enough Digits for the base", nameof(digits))); }


            var result = new StringBuilder();
            var sign = 1;

            if (subject < 0) { subject *= sign = -1; }

            do
            {
                result.Insert(0, digits[(int)(subject % @base)]);
                subject /= @base;
            }
            while (subject > 0);

            if (sign == -1)
            {
                result.Insert(0, '-');
            }

            return (result.ToString());
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureCreated(Option<TypedPsObject<VirtualMachineInfo>> vmInfo, MachineConfig config, VMStorageSettings storageSettings, IPowershellEngine engine)
        {
            return vmInfo.MatchAsync(
                None: () =>
                    storageSettings.StorageIdentifier.ToEither(new PowershellFailure{Message = "Unknown storage identifier, cannot create new virtual machine"})
                        .BindAsync(storageIdentifier => Converge.CreateVirtualMachine(engine, config.Name, storageIdentifier,
                            storageSettings.VMPath,
                            config.VM.Memory.Startup)),                            
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
                    None: () => vmInfo,
                    Some: (some) => Converge.RenameVirtualMachine(engine, vmInfo, config.Name));


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
            IPowershellEngine engine) =>

            Prelude.Cond<string>((c) => !string.IsNullOrWhiteSpace(c))(id).MatchAsync(
                None:  () => Seq<TypedPsObject<VirtualMachineInfo>>.Empty,
                Some: (s) => engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm").AddParameter("Id", s)
                    //this a bit dangerous, because there may be other errors causing the 
                    //command to fail. However there seems to be no other way except parsing error response
                    .AddParameter("ErrorAction", "SilentlyContinue")
                ));

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

}