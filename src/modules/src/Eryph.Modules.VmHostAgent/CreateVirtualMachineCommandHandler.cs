using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Messages.Resources.Catlets.Commands;
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

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class CreateVirtualMachineCommandHandler : 
        VirtualCatletConfigCommandHandler<CreateVirtualCatletCommand, ConvergeVirtualCatletResult>
    {
        private readonly IHostInfoProvider _hostInfoProvider;

        public CreateVirtualMachineCommandHandler(IPowershellEngine engine, IBus bus, ILogger log, IHostInfoProvider hostInfoProvider) : base(engine, bus, log)
        {
            _hostInfoProvider = hostInfoProvider;
        }

        protected override EitherAsync<Error, ConvergeVirtualCatletResult> HandleCommand(CreateVirtualCatletCommand command)
        {
            var config = command.Config;

            var hostSettings = HostSettingsBuilder.GetHostSettings();

            var planStorageSettings = Prelude.fun(() =>
                VMStorageSettings.Plan(hostSettings, LongToString(command.StorageId), config,
                    Option<VMStorageSettings>.None));

            var getTemplate = Prelude.fun(() =>
                GetTemplate(Engine, hostSettings, config.VCatlet.Image));

            var createVM = Prelude.fun((VMStorageSettings settings, TypedPsObject<PlannedVirtualMachineInfo> template) =>
                CreateVM(config, hostSettings, settings, Engine, template));

            var createMetadata = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, TypedPsObject<PlannedVirtualMachineInfo> plannedVM) =>
                    CreateMetadata(plannedVM, vmInfo, config, command.NewMachineId));

            return
                from plannedStorageSettings in planStorageSettings()
                from template in getTemplate()
                from createdVM in createVM(plannedStorageSettings, template)
                from metadata in createMetadata(createdVM, template)
                from inventory in CreateMachineInventory(Engine, hostSettings, createdVM, _hostInfoProvider)
                select new ConvergeVirtualCatletResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata
                };
        }

        private static EitherAsync<Error, TypedPsObject<PlannedVirtualMachineInfo>> GetTemplate(
            IPowershellEngine engine,
            HostSettings hostSettings,
            string image)
        {
            //add a cache lookup here, as image data should not change
            return VirtualMachine.TemplateFromImage(engine, hostSettings, image);
        }

        private static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> CreateVM(CatletConfig config,
            HostSettings hostSettings, VMStorageSettings storageSettings, IPowershellEngine engine,
            TypedPsObject<PlannedVirtualMachineInfo> template)
        {
            return (from storageIdentifier in storageSettings.StorageIdentifier.ToEitherAsync(Error.New(
                    "Unknown storage identifier, cannot create new virtual catlet"))
                from vm in VirtualMachine.ImportTemplate(engine, hostSettings, config.Name,
                    storageIdentifier,
                    storageSettings.VMPath,
                    template)
                select vm);
        }

        private EitherAsync<Error, VirtualCatletMetadata> CreateMetadata(
            TypedPsObject<PlannedVirtualMachineInfo> template,
            TypedPsObject<VirtualMachineInfo> vmInfo, CatletConfig config, Guid machineId)
        {
            var metadata = new VirtualCatletMetadata
            {
                Id = Guid.NewGuid(),
                MachineId = machineId,
                VMId = vmInfo.Value.Id,
                RaisingConfig = config.Raising,
                ImageConfig = template.ToVmConfig()
        };

            return SetMetadataId(vmInfo, metadata.Id).Map(_ => metadata);
        }
    }
}