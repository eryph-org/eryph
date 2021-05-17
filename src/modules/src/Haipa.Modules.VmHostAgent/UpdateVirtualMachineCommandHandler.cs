using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Events;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;
using Haipa.VmManagement;
using Haipa.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using VirtualMachineInfo = Haipa.VmManagement.Data.Full.VirtualMachineInfo;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class UpdateVirtualMachineCommandHandler : VirtualMachineConfigCommandHandler,
        IHandleMessages<AcceptedOperationTaskEvent<UpdateVirtualMachineCommand>>
    {

        public UpdateVirtualMachineCommandHandler(IPowershellEngine engine, IBus bus) : base(engine, bus)
        {
        }

        public Task Handle(AcceptedOperationTaskEvent<UpdateVirtualMachineCommand> message)
        {
            var command = message.Command;
            var config = command.Config;
            var vmId = command.VMId;

            OperationId = command.OperationId;
            TaskId = command.TaskId;

            var hostSettings = HostSettingsBuilder.GetHostSettings();
            var convergeVM = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig c, VMStorageSettings storageSettings) =>
                    VirtualMachine.Converge(hostSettings, Engine, ProgressMessage, vmInfo, c, storageSettings));

            var chain =

                from vmList in GetVmInfo(vmId, Engine).ToAsync()
                from vmInfo in EnsureSingleEntry(vmList, vmId).ToAsync()

                from currentStorageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo).ToAsync()
                from plannedStorageSettings in VMStorageSettings.Plan(hostSettings, LongToString(command.NewStorageId),
                    config, currentStorageSettings).ToAsync()

                from metadata in EnsureMetadata(message.Command.MachineMetadata, vmInfo).ToAsync()
                from mergedConfig in config.MergeWithImageSettings(metadata.ImageConfig).ToAsync()
                from vmInfoConsistent in EnsureNameConsistent(vmInfo, config, Engine).ToAsync()

                from vmInfoConverged in convergeVM(vmInfoConsistent, mergedConfig, plannedStorageSettings).ToAsync()
                from inventory in CreateMachineInventory(Engine, hostSettings, vmInfoConverged).ToAsync()
                select new ConvergeVirtualMachineResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata,
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