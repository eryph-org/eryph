using System;
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
                from machineConfig in Converge.NormalizeMachineConfig(config, GetHostSettings(), _engine, ProgressMessage)
                from vmList in GetVmInfo(machineConfig.Name, _engine)
                from optionalVmInfo in EnsureUnique(vmList, machineConfig.Name)
                from vmInfo in EnsureCreated(optionalVmInfo, machineConfig, _engine) 
                from _ in AttachToOperation(vmInfo, _bus, command.OperationId)
                from vmInfoConverged in ConvergeVm(vmInfo, machineConfig, _engine)
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

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> ConvergeVm(TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig machineConfig, IPowershellEngine engine)
        {
            return
                from infoFirmware in Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                from infoCpu in Converge.Cpu(infoFirmware, machineConfig.VM.Cpu, engine, ProgressMessage)
                from infoDisks in Converge.Disks(infoCpu, machineConfig.VM.Disks.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoNetworks in Converge.NetworkAdapters(infoDisks, machineConfig.VM.NetworkAdapters.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoCloudInit in Converge.CloudInit(infoNetworks, machineConfig.VM.Path, machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)
                select infoCloudInit;
                
                //Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                //.BindAsync(info => Converge.Cpu(info, machineConfig.VMConfig.Cpu, engine, ProgressMessage))
                //.BindAsync(info => Converge.Disks(info, machineConfig.VMConfig.Disks?.ToSeq(), machineConfig, engine, ProgressMessage))

                //.BindAsync(info => Converge.CloudInit(
                //    info, machineConfig.VMConfig.Path, machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)).ConfigureAwait(false);

            //await Converge.Definition(engine, vmInfo, config, ProgressMessage).ConfigureAwait(false);
            //await ProgressMessage("Generate Virtual Machine provisioning disk").ConfigureAwait(false);

        }

        private async Task<Either<PowershellFailure, Option<TypedPsObject<VirtualMachineInfo>>>> EnsureUnique(Seq<TypedPsObject<VirtualMachineInfo>> list, string vmName)
        {

            if (list.Count > 1)
                return Prelude.Left(new PowershellFailure { Message = $"VM name '{vmName}' is not unique." });

            return Prelude.Right(list.HeadOrNone());
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureCreated(Option<TypedPsObject<VirtualMachineInfo>> vmInfo, MachineConfig machineConfig, IPowershellEngine engine)
        {
            return vmInfo.MatchAsync(
                None: () => Converge.CreateVirtualMachine(engine, machineConfig.Name,
                    machineConfig.VM.Path,
                    machineConfig.VM.Memory.Startup),
                Some: s => s
            );

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

        private static Task<Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(string vmName,
            IPowershellEngine engine)
        {
            return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                .AddCommand("get-vm").AddArgument(vmName)
                //this a bit dangerous, because there may be other errors causing the 
                //command to fail. However there seems to be no other way except parsing error response
                .AddParameter("ErrorAction", "SilentlyContinue")
            );
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
}