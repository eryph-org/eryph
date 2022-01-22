using System;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Resources.Machines;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Rebus.Bus;
using Rebus.Handlers;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class CreateVirtualMachineCommandHandler : VirtualMachineConfigCommandHandler,
        IHandleMessages<OperationTask<CreateVirtualMachineCommand>>
    {
        public CreateVirtualMachineCommandHandler(IPowershellEngine engine, IBus bus) : base(engine, bus)
        {
        }


        public Task Handle(OperationTask<CreateVirtualMachineCommand> message)
        {
            var command = message.Command;
            var config = command.Config;

            OperationId = message.OperationId;
            TaskId = message.TaskId;

            var hostSettings = HostSettingsBuilder.GetHostSettings();

            var planStorageSettings = Prelude.fun(() =>
                VMStorageSettings.Plan(hostSettings, LongToString(message.Command.StorageId), config,
                    Option<VMStorageSettings>.None).ToAsync());

            var getTemplate = Prelude.fun(() =>
                GetTemplate(Engine, hostSettings, config.Image).ToAsync());

            var createVM = Prelude.fun((VMStorageSettings settings, Option<PlannedVirtualMachineInfo> template) =>
                CreateVM(config, hostSettings, settings, Engine, template).ToAsync());

            var createMetadata = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, Option<PlannedVirtualMachineInfo> plannedVM) =>
                    CreateMetadata(plannedVM, vmInfo, config, command.NewMachineId).ToAsync());

            var chain =
                from plannedStorageSettings in planStorageSettings()
                from optionalTemplate in getTemplate()
                from createdVM in createVM(plannedStorageSettings, optionalTemplate)
                from metadata in createMetadata(createdVM, optionalTemplate)
                from inventory in CreateMachineInventory(Engine, hostSettings, createdVM).ToAsync()
                select new ConvergeVirtualMachineResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata
                };

            return chain.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: result =>
                    Bus.Publish(OperationTaskStatusEvent.Completed(OperationId, TaskId, result)).ToUnit());
        }

        private static Task<Either<PowershellFailure, Option<PlannedVirtualMachineInfo>>> GetTemplate(
            IPowershellEngine engine,
            HostSettings hostSettings,
            MachineImageConfig imageConfig)
        {
            //add a cache lookup here, as image data should not change
            return VirtualMachine.TemplateFromImage(engine, hostSettings, imageConfig);
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> CreateVM(MachineConfig config,
            HostSettings hostSettings, VMStorageSettings storageSettings, IPowershellEngine engine,
            Option<PlannedVirtualMachineInfo> optionalTemplate)
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

        private Task<Either<PowershellFailure, VirtualMachineMetadata>> CreateMetadata(
            Option<PlannedVirtualMachineInfo> template,
            TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig config, Guid machineId)
        {
            var metadata = new VirtualMachineMetadata
            {
                Id = Guid.NewGuid(),
                MachineId = machineId,
                VMId = vmInfo.Value.Id,
                ProvisioningConfig = config.Provisioning
            };

            if (template.IsSome)
                metadata.ImageConfig = template.ValueUnsafe().ToVmConfig();


            return SetMetadataId(vmInfo, metadata.Id).MapAsync(u => metadata);
        }
    }
}