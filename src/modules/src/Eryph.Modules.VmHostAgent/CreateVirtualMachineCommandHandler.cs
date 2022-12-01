using System;
using System.Threading.Tasks;
using Eryph.ConfigModel.Machine;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class CreateVirtualMachineCommandHandler : VirtualMachineConfigCommandHandler,
        IHandleMessages<OperationTask<CreateVirtualMachineCommand>>
    {
        private readonly IHostInfoProvider _hostInfoProvider;

        public CreateVirtualMachineCommandHandler(IPowershellEngine engine, IBus bus, ILogger log, IHostInfoProvider hostInfoProvider) : base(engine, bus, log)
        {
            _hostInfoProvider = hostInfoProvider;
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
                    Option<VMStorageSettings>.None));

            var getTemplate = Prelude.fun(() =>
                GetTemplate(Engine, hostSettings, config.VM.Image));

            var createVM = Prelude.fun((VMStorageSettings settings, TypedPsObject<PlannedVirtualMachineInfo> template) =>
                CreateVM(config, hostSettings, settings, Engine, template));

            var createMetadata = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, TypedPsObject<PlannedVirtualMachineInfo> plannedVM) =>
                    CreateMetadata(plannedVM, vmInfo, config, command.NewMachineId));

            var chain =
                from plannedStorageSettings in planStorageSettings()
                from template in getTemplate()
                from createdVM in createVM(plannedStorageSettings, template)
                from metadata in createMetadata(createdVM, template)
                from inventory in CreateMachineInventory(Engine, hostSettings, createdVM, _hostInfoProvider)
                select new ConvergeVirtualMachineResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata
                };

            return chain.Match(
                Left: HandleError,
                Right: result =>
                    Bus.Publish(OperationTaskStatusEvent.Completed(OperationId, TaskId, result)).ToUnit());
        }

        private static EitherAsync<Error, TypedPsObject<PlannedVirtualMachineInfo>> GetTemplate(
            IPowershellEngine engine,
            HostSettings hostSettings,
            string image)
        {
            //add a cache lookup here, as image data should not change
            return VirtualMachine.TemplateFromImage(engine, hostSettings, image);
        }

        private static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> CreateVM(MachineConfig config,
            HostSettings hostSettings, VMStorageSettings storageSettings, IPowershellEngine engine,
            TypedPsObject<PlannedVirtualMachineInfo> template)
        {
            return (from storageIdentifier in storageSettings.StorageIdentifier.ToEitherAsync(Error.New(
                    "Unknown storage identifier, cannot create new virtual machine"))
                from vm in VirtualMachine.ImportTemplate(engine, hostSettings, config.Name,
                    storageIdentifier,
                    storageSettings.VMPath,
                    template)
                select vm);
        }

        private EitherAsync<Error, VirtualMachineMetadata> CreateMetadata(
            TypedPsObject<PlannedVirtualMachineInfo> template,
            TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig config, Guid machineId)
        {
            var metadata = new VirtualMachineMetadata
            {
                Id = Guid.NewGuid(),
                MachineId = machineId,
                VMId = vmInfo.Value.Id,
                ProvisioningConfig = config.Provisioning,
                ImageConfig = template.ToVmConfig()
        };

            return SetMetadataId(vmInfo, metadata.Id).Map(_ => metadata);
        }
    }
}