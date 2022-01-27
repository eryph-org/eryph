using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Resources.Machines;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class UpdateVirtualMachineCommandHandler : VirtualMachineConfigCommandHandler,
        IHandleMessages<OperationTask<UpdateVirtualMachineCommand>>
    {
        private readonly IHostInfoProvider _hostInfoProvider;

        public UpdateVirtualMachineCommandHandler(IPowershellEngine engine, IBus bus, ILogger log, IHostInfoProvider hostInfoProvider) : base(engine, bus, log)
        {
            _hostInfoProvider = hostInfoProvider;
        }

        public Task Handle(OperationTask<UpdateVirtualMachineCommand> message)
        {
            var command = message.Command;
            var config = command.Config;
            var vmId = command.VMId;

            OperationId = message.OperationId;
            TaskId = message.TaskId;

            var hostSettings = HostSettingsBuilder.GetHostSettings();
            var convergeVM = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig c, VMStorageSettings storageSettings, VMHostMachineData hostInfo) =>
                    VirtualMachine.Converge(hostSettings, hostInfo,Engine, ProgressMessage, vmInfo, c, storageSettings));

            var chain =
                from hostInfo in _hostInfoProvider.GetHostInfoAsync().ToAsync()
                from vmList in GetVmInfo(vmId, Engine).ToAsync()
                from vmInfo in EnsureSingleEntry(vmList, vmId).ToAsync()
                from currentStorageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo).ToAsync()
                from plannedStorageSettings in VMStorageSettings.Plan(hostSettings, LongToString(command.NewStorageId),
                    config, currentStorageSettings).ToAsync()
                from metadata in EnsureMetadata(message.Command.MachineMetadata, vmInfo).ToAsync()
                from mergedConfig in config.MergeWithImageSettings(metadata.ImageConfig).ToAsync()
                from vmInfoConsistent in EnsureNameConsistent(vmInfo, config, Engine).ToAsync()
                from vmInfoConverged in convergeVM(vmInfoConsistent, mergedConfig, plannedStorageSettings, hostInfo).ToAsync()
                from inventory in CreateMachineInventory(Engine, hostSettings, vmInfoConverged, _hostInfoProvider).ToAsync()
                select new ConvergeVirtualMachineResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata
                };
            return chain.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: async result =>
                {
                    await ProgressMessage($"Virtual machine '{result.Inventory.Name}' has been converged.")
                        .ConfigureAwait(false);

                    return await Bus.Publish(OperationTaskStatusEvent.Completed(OperationId, TaskId, result))
                        .ToUnit().ConfigureAwait(false);
                });
        }
    }
}