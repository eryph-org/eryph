using System.IO;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using Haipa.VmManagement.Data.Planned;
using Haipa.VmManagement.Storage;
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
                VMStorageSettings.Plan(hostSettings, GenerateId, config, Option<VMStorageSettings>.None).ToAsync());

            var getTemplate = Prelude.fun(() =>
                GetTemplate(_engine, hostSettings,config.Image).ToAsync());
            
            var createVM = Prelude.fun((VMStorageSettings settings, Option<PlannedVirtualMachineInfo> template) => 
                CreateVM(config, hostSettings, settings, _engine, template).ToAsync());

            var createMetadata = Prelude.fun((TypedPsObject<VirtualMachineInfo> vmInfo, Option<PlannedVirtualMachineInfo> plannedVM) =>
                CreateMetadata(plannedVM, vmInfo, config).ToAsync());

            var chain =
                from plannedStorageSettings in planStorageSettings()
                from optionalTemplate in getTemplate()
                from createdVM in createVM(plannedStorageSettings, optionalTemplate)
                from metadata in createMetadata(createdVM, optionalTemplate)
                from inventory in CreateMachineInventory(_engine, hostSettings, createdVM).ToAsync()
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

        private static Task<Either<PowershellFailure, Option<PlannedVirtualMachineInfo>>> GetTemplate(
            IPowershellEngine engine,
            HostSettings hostSettings,
            MachineImageConfig imageConfig)
        {
            //add a cache lookup here, as image data should not change
            return VirtualMachine.TemplateFromImage(engine, hostSettings, imageConfig);
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> CreateVM(MachineConfig config, HostSettings hostSettings, VMStorageSettings storageSettings, IPowershellEngine engine, Option<PlannedVirtualMachineInfo> optionalTemplate)
        {
            return from storageIdentifier in storageSettings.StorageIdentifier.ToEitherAsync(new PowershellFailure
                    {Message = "Unknown storage identifier, cannot create new virtual machine"}).ToEither()

                from vm in optionalTemplate.MatchAsync(
                    Some: template =>

                        VirtualMachine.ImportTemplate(engine, hostSettings, config.Name,
                            storageIdentifier,
                            storageSettings.VMPath,
                            template),

                    None: () =>
                        VirtualMachine.Create(engine, config.Name, storageIdentifier,
                            storageSettings.VMPath,
                            config.VM.Memory.Startup)
                        )

                        select vm;

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