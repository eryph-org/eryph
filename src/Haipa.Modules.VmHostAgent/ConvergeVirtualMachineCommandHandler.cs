using System;
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

            return GetVmInfo(config.Name, _engine)
                .BindAsync(l => EnsureUnique(l, config.Name))
                .BindAsync(l => EnsureCreated(l, config, _engine))
                .BindAsync(vmInfo => AttachToOperation(vmInfo, _bus, command.OperationId))
                .BindAsync(vmInfo => ConvergeVm(vmInfo, config, _engine))
                .ToAsync()
                .MatchAsync(
                    LeftAsync: HandleError,
                    RightAsync: vmInfo => _bus.Send(new OperationCompletedEvent
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
                from config in Converge.NormalizeMachineConfig(vmInfo, machineConfig, engine, ProgressMessage)
                from infoFirmware in Converge.Firmware(vmInfo, config, engine, ProgressMessage)
                from infoCpu in Converge.Cpu(infoFirmware, config.VM.Cpu, engine, ProgressMessage)
                from infoDisks in Converge.Disks(infoCpu, config.VM.Disks.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoNetworks in Converge.NetworkAdapters(infoDisks, config.VM.NetworkAdapters.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoCloudInit in Converge.CloudInit(infoNetworks, config.VM.Path, machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)
                select infoCloudInit;
                
                //Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                //.BindAsync(info => Converge.Cpu(info, machineConfig.VMConfig.Cpu, engine, ProgressMessage))
                //.BindAsync(info => Converge.Disks(info, machineConfig.VMConfig.Disks?.ToSeq(), machineConfig, engine, ProgressMessage))

                //.BindAsync(info => Converge.CloudInit(
                //    info, machineConfig.VMConfig.Path, machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)).ConfigureAwait(false);

            //await Converge.Definition(engine, vmInfo, config, ProgressMessage).ConfigureAwait(false);
            //await ProgressMessage("Generate Virtual Machine provisioning disk").ConfigureAwait(false);

        }

        private Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>> EnsureUnique(Seq<TypedPsObject<VirtualMachineInfo>> list, string vmName)
        {

            if (list.Count > 1)
                return Prelude.Left(new PowershellFailure { Message = $"VM name '{vmName}' is not unique." });

            return Prelude.Right(list);
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureCreated(Seq<TypedPsObject<VirtualMachineInfo>> list, MachineConfig machineConfig, IPowershellEngine engine)
        {
            return list.HeadOrNone().MatchAsync(
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

        private static async Task<Unit> HandleError(PowershellFailure failure)
        {
            return Unit.Default;
        }


    }
}