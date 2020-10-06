using System;
using System.IO;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using Haipa.VmManagement.Data.Planned;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Handlers;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class CreateVirtualMachineCommandHandler : VirtualMachineConfigCommandHandler, IHandleMessages<AcceptedOperationTask<CreateVirtualMachineCommand>>
    {
        public CreateVirtualMachineCommandHandler(IPowershellEngine engine, IBus bus) : base(engine, bus)
        {
        }


        public Task Handle(AcceptedOperationTask<CreateVirtualMachineCommand> message)
        {
            var command = message.Command;
            var config = command.Config;

            _operationId = command.OperationId;
            _taskId = command.TaskId;

            var hostSettings = HostSettingsBuilder.GetHostSettings();
            
            var planStorageSettings = Prelude.fun(() => 
                Storage.PlanVMStorageSettings(config, Option<VMStorageSettings>.None, hostSettings, GenerateId).ToAsync());

            var createVM = Prelude.fun((VMStorageSettings settings) => 
                CreateVM(config, hostSettings, settings, _engine).ToAsync());

            var createMetadata = Prelude.fun((TypedPsObject<VirtualMachineInfo> vmInfo, Option<PlannedVirtualMachineInfo> plannedVM) =>
                CreateMetadata(plannedVM, vmInfo, config).ToAsync());

            var chain =
                from plannedStorageSettings in planStorageSettings()
                from creationInfo in createVM(plannedStorageSettings)
                from metadata in createMetadata(creationInfo.VM, creationInfo.Template)
                from inventory in CreateMachineInventory(creationInfo.VM).ToAsync()
                select new CreateVirtualMachineResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata,
                };

            return chain.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: result => 
                    _bus.Publish(OperationTaskStatusEvent.Completed(_operationId, _taskId, result)).ToUnit());

        }


        private static Task<Either<PowershellFailure, (TypedPsObject<VirtualMachineInfo> VM, Option<PlannedVirtualMachineInfo> Template)>> CreateVM(MachineConfig config, HostSettings hostSettings, VMStorageSettings storageSettings, IPowershellEngine engine)
        {
            if (!string.IsNullOrWhiteSpace(config.Image.Name))
            {
                return storageSettings.StorageIdentifier.ToEither(new PowershellFailure
                        {Message = "Unknown storage identifier, cannot create new virtual machine"})
                    .BindAsync(storageIdentifier => Converge.ImportVirtualMachine(engine, hostSettings, config.Name,
                        storageIdentifier,
                        storageSettings.VMPath,
                        config.Image));


            }

            return storageSettings.StorageIdentifier.ToEither(new PowershellFailure
                    {Message = "Unknown storage identifier, cannot create new virtual machine"})
                .BindAsync(storageIdentifier => Converge.CreateVirtualMachine(engine, config.Name, storageIdentifier,
                    storageSettings.VMPath,
                    config.VM.Memory.Startup)).MapAsync(r => (r, Option<PlannedVirtualMachineInfo>.None));

        }

        private Task<Either<PowershellFailure, VirtualMachineMetadata>> CreateMetadata(Option<PlannedVirtualMachineInfo> template,
            TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig config)
        {
            return GenerateId().BindAsync(id =>
            {
                var metadata = new VirtualMachineMetadata
                {
                    Id = id,
                    VMId = vmInfo.Value.Id,
                    ProvisioningConfig = config.Provisioning
                };

                if (template.IsSome)
                    metadata.ImageConfig = template.ValueUnsafe().ToVmConfig();

                var metadataJson = JsonConvert.SerializeObject(metadata);
                File.WriteAllText($"{metadata.Id}.hmeta", metadataJson);

                return Prelude.RightAsync<PowershellFailure, VirtualMachineMetadata>(metadata).ToEither();
            }).BindAsync(metadata =>
            {
                var newNotes = $"Haipa metadata id: {metadata.Id}";

                return _engine.RunAsync(new PsCommandBuilder().AddCommand("Set-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Notes", newNotes)).MapAsync(u => metadata);
            });



        }
    }


}