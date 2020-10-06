using System;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
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

            var chain =

                from vmList in GetVmInfo(machineId, _engine).ToAsync()
                from vmInfo in EnsureSingleEntry(vmList, machineId).ToAsync()

                from currentStorageSettings in Storage.DetectVMStorageSettings(vmInfo, hostSettings, ProgressMessage).ToAsync()
                from plannedStorageSettings in Storage.PlanVMStorageSettings(config, currentStorageSettings, hostSettings, GenerateId).ToAsync()

                from metadata in EnsureMetadata(message.Command.MachineMetadata, vmInfo).ToAsync()
                from mergedConfig in Converge.MergeConfigAndImageSettings(metadata.ImageConfig, config, _engine).ToAsync()
                from vmInfoConsistent in EnsureNameConsistent(vmInfo, config, _engine).ToAsync()

                from vmInfoConverged in ConvergeVm(vmInfoConsistent, mergedConfig, plannedStorageSettings, hostSettings, _engine).ToAsync()
                select vmInfoConverged;

            return chain.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: async vmInfo2 =>
                {
                    await ProgressMessage($"Virtual machine '{vmInfo2.Value.Name}' has been converged.").ConfigureAwait(false);

                    return await _bus.Publish(OperationTaskStatusEvent.Completed(_operationId, _taskId))
                        .ToUnit().ConfigureAwait(false);
                });

        }

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> ConvergeVm(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            MachineConfig machineConfig,
            VMStorageSettings storageSettings,
            HostSettings hostSettings,
            IPowershellEngine engine)
        {
            return
                from infoFirmware in Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                from infoCpu in Converge.Cpu(infoFirmware, machineConfig.VM.Cpu, engine, ProgressMessage)
                from infoDrives in Converge.Drives(infoCpu, machineConfig, storageSettings, hostSettings, engine, ProgressMessage)
                from infoNetworks in Converge.NetworkAdapters(infoDrives, machineConfig.VM.NetworkAdapters.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoCloudInit in Converge.CloudInit(infoNetworks, storageSettings.VMPath, machineConfig.Provisioning, engine, ProgressMessage)
                select infoCloudInit;

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