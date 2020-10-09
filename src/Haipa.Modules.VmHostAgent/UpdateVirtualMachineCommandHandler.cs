using System;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data.Full;
using Haipa.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class UpdateVirtualMachineCommandHandler : VirtualMachineConfigCommandHandler, IHandleMessages<AcceptedOperationTask<UpdateVirtualMachineCommand>>
    {

        public UpdateVirtualMachineCommandHandler(IPowershellEngine engine, IBus bus) : base(engine, bus)
        {
        }

        public Task Handle(AcceptedOperationTask<UpdateVirtualMachineCommand> message)
        {
            var command = message.Command;
            var config = command.Config;
            var machineId = command.MachineId;

            _operationId = command.OperationId;
            _taskId = command.TaskId;

            var hostSettings = HostSettingsBuilder.GetHostSettings();
            var convergeVM = Prelude.fun((TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig c, VMStorageSettings storageSettings) =>
                VirtualMachine.Converge(hostSettings, _engine, ProgressMessage, vmInfo, c, storageSettings));

            var chain =

                from vmList in GetVmInfo(machineId, _engine).ToAsync()
                from vmInfo in EnsureSingleEntry(vmList, machineId).ToAsync()

                from currentStorageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo).ToAsync()
                from plannedStorageSettings in VMStorageSettings.Plan(hostSettings, GenerateId, config, currentStorageSettings).ToAsync()

                from metadata in EnsureMetadata(message.Command.MachineMetadata, vmInfo).ToAsync()
                from mergedConfig in config.MergeWithImageSettings(metadata.ImageConfig).ToAsync()
                from vmInfoConsistent in EnsureNameConsistent(vmInfo, config, _engine).ToAsync()

                from vmInfoConverged in convergeVM(vmInfoConsistent, mergedConfig, plannedStorageSettings).ToAsync()
                from inventory in CreateMachineInventory(_engine, hostSettings, vmInfoConverged).ToAsync()
                select new ConvergeVirtualMachineResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata,
                };
            return chain.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: async result =>
                {
                    await ProgressMessage($"Virtual machine '{result.Inventory.Name}' has been converged.").ConfigureAwait(false);

                    return await _bus.Publish(OperationTaskStatusEvent.Completed(_operationId, _taskId, result))
                        .ToUnit().ConfigureAwait(false);
                });

        }
        
#pragma warning disable 1998
        private async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureSingleEntry(Seq<TypedPsObject<VirtualMachineInfo>> list, Guid id)
#pragma warning restore 1998
        {
            return list.Count > 1 
                ? Prelude.Left(new PowershellFailure { Message = $"VM id '{id}' is not unique." }) 
                : list.HeadOrNone().ToEither(new PowershellFailure {Message = $"VM id '{id}' is not found."});
        }

        
        private static Task<Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(Guid id,
            IPowershellEngine engine) =>

            Prelude.Cond<Guid>((c) => c != Guid.Empty)(id).MatchAsync(
                None: () => Seq<TypedPsObject<VirtualMachineInfo>>.Empty,
                Some: (s) => engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm").AddParameter("Id", id)
                    //this a bit dangerous, because there may be other errors causing the 
                    //command to fail. However there seems to be no other way except parsing error response
                    .AddParameter("ErrorAction", "SilentlyContinue")
                ));


        private Task<Either<PowershellFailure, VirtualMachineMetadata>> EnsureMetadata(VirtualMachineMetadata metadata, TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var notes = vmInfo.Value.Notes;

            var metadataId = "";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                var metadataIndex = notes.IndexOf("Haipa metadata id: ", StringComparison.InvariantCultureIgnoreCase);
                if (metadataIndex != -1)
                {
                    var metadataEnd = metadataIndex + "Haipa metadata id: ".Length + 36;
                    if (metadataEnd <= notes.Length)
                        metadataId = notes.Substring(metadataIndex + "Haipa metadata id: ".Length, 36);

                }
            }

            if (string.IsNullOrWhiteSpace(metadataId))
            {
                var newNotes = $"Haipa metadata id: {metadata.Id}";

                return _engine.RunAsync(new PsCommandBuilder().AddCommand("Set-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Notes", newNotes)).MapAsync(u => metadata);

            }


            if (metadataId != metadata.Id)
                throw new InvalidOperationException("Inconsistent metadata id between VM and expected metadata id.");

            return Prelude.RightAsync<PowershellFailure, VirtualMachineMetadata>(metadata).ToEither();
        }

    }

    
}