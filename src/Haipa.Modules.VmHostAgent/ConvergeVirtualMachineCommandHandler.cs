using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.VmConfig;
using HyperVPlus.VmManagement;
using HyperVPlus.VmManagement.Data;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Haipa.Modules.VmHostAgent
{
    internal class ConvergeTaskRequestedEventHandler : IHandleMessages<ConvergeVirtualMachineRequestedEvent>
    {
        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;
        private Guid _correlationid;

        public ConvergeTaskRequestedEventHandler(
            IPowershellEngine engine,
            IBus bus)
        {
            _engine = engine;
            _bus = bus;
        }

        public async Task Handle(ConvergeVirtualMachineRequestedEvent command)
        {
            _correlationid = command.CorellationId;
            var config = command.Config;

            Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>> BindableEnsureUnique(
                Seq<TypedPsObject<VirtualMachineInfo>> list) => EnsureUnique(list, config.Name);

            Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> BindableTaskEnsureCreated(
                Seq<TypedPsObject<VirtualMachineInfo>> list) => EnsureCreated(list, config, _engine);

            Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> BindableAttachToOperation(
                TypedPsObject<VirtualMachineInfo> vmInfo) => AttachToOperation(vmInfo, _bus, _correlationid);

            //Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> BindableConvergeVm(
            //    TypedPsObject<VirtualMachineInfo> vmInfo) => ConvergeVm(vmInfo, config, engine);


            var result = await GetVmInfo(config.Name, _engine)
                .BindAsync(BindableEnsureUnique)
                .BindAsync(BindableTaskEnsureCreated)
                .BindAsync(BindableAttachToOperation)
                .BindAsync(vmInfo => ConvergeVm(vmInfo, config, _engine)).ConfigureAwait(false);

            await result.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: async vmInfo =>
                {
                    await _bus.Send(new VirtualMachineConvergedEvent
                    {
                        CorellationId = _correlationid,
                        Inventory = VmToInventory(vmInfo.Recreate())

                    }).ConfigureAwait(false);

                    return Unit.Default;
                }).ConfigureAwait(false);




            //await _bus.SendLocal(result.ToEvent(command.CorellationId));

        }

        private async Task<Unit> ProgressMessage(string message)
        {
            using (var scope = new RebusTransactionScope())
            {
                await _bus.Send(new ConvergeVirtualMachineProgressEvent
                {
                    CorellationId = _correlationid,
                    Message = message
                }).ConfigureAwait(false);

                // commit it like this
                await scope.CompleteAsync().ConfigureAwait(false);
            }
            return Unit.Default;

        }

        private async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> ConvergeVm(TypedPsObject<VirtualMachineInfo> vmInfo, VirtualMachineConfig config, IPowershellEngine engine)
        {
            var result = await Converge.Firmware(vmInfo, config, engine, ProgressMessage)
                .BindAsync(info => Converge.Cpu(info, config.Cpu, engine, ProgressMessage))
                .BindAsync(info => Converge.Disks(info, config.Disks?.ToSeq(), config, engine, ProgressMessage))

                .BindAsync(info => Converge.CloudInit(
                    info, config.Path, config.Hostname, config.Provisioning?.UserData, engine, ProgressMessage)).ConfigureAwait(false);

            //await Converge.Definition(engine, vmInfo, config, ProgressMessage).ConfigureAwait(false);

            config.Networks?.Iter(async (network) =>
                await Converge.Network(engine, vmInfo, network, config, ProgressMessage)
                    .ConfigureAwait(false));

            //await ProgressMessage("Generate Virtual Machine provisioning disk").ConfigureAwait(false);


            return result;
        }

        private static VmInventoryInfo VmToInventory(TypedPsObject<VirtualMachineInfo> vm)
        {
            return new VmInventoryInfo
            {
                Id = vm.Value.Id,
                Status = MapVmInfoStatusToVmStatus(vm.Value.State),
                Name = vm.Value.Name,
                IpV4Addresses = GetAddressesByFamily(vm, AddressFamily.InterNetwork),
                IpV6Addresses = GetAddressesByFamily(vm, AddressFamily.InterNetworkV6),
            };
        }

        private static VmStatus MapVmInfoStatusToVmStatus(VirtualMachineState valueState)
        {
            switch (valueState)
            {
                case VirtualMachineState.Other:
                    return VmStatus.Stopped;
                case VirtualMachineState.Running:
                    return VmStatus.Running;
                case VirtualMachineState.Off:
                    return VmStatus.Stopped;
                case VirtualMachineState.Stopping:
                    return VmStatus.Stopped;
                case VirtualMachineState.Saved:
                    return VmStatus.Stopped;
                case VirtualMachineState.Paused:
                    return VmStatus.Stopped;
                case VirtualMachineState.Starting:
                    return VmStatus.Stopped;
                case VirtualMachineState.Reset:
                    return VmStatus.Stopped;
                case VirtualMachineState.Saving:
                    return VmStatus.Stopped;
                case VirtualMachineState.Pausing:
                    return VmStatus.Stopped;
                case VirtualMachineState.Resuming:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSaved:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSaving:
                    return VmStatus.Stopped;
                case VirtualMachineState.ForceShutdown:
                    return VmStatus.Stopped;
                case VirtualMachineState.ForceReboot:
                    return VmStatus.Stopped;
                case VirtualMachineState.RunningCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.OffCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.StoppingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.SavedCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.PausedCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.StartingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.ResetCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.SavingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.PausingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.ResumingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSavedCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSavingCritical:
                    return VmStatus.Stopped;
                default:
                    throw new ArgumentOutOfRangeException(nameof(valueState), valueState, null);
            }
        }

        private static List<string> GetAddressesByFamily(TypedPsObject<VirtualMachineInfo> vm, AddressFamily family)
        {
            return vm.Value.NetworkAdapters.Bind(adapter => adapter.IPAddresses.Where(a =>
            {
                var ipAddress = IPAddress.Parse(a);
                return ipAddress.AddressFamily == family;
            })).ToList();
        }



        private Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>> EnsureUnique(Seq<TypedPsObject<VirtualMachineInfo>> list, string vmName)
        {

            if (list.Count > 1)
                return Prelude.Left(new PowershellFailure { Message = $"VM name '{vmName}' is not unique." });

            return Prelude.Right(list);
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureCreated(Seq<TypedPsObject<VirtualMachineInfo>> list, VirtualMachineConfig config, IPowershellEngine engine)
        {
            return list.HeadOrNone().MatchAsync(
                None: () => Converge.CreateVirtualMachine(engine, config.Name,
                    config.Path,
                    config.Memory.Startup),
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