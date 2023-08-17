using System;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class CreateVCatletCommandHandler : 
        VirtualCatletConfigCommandHandler<CreateVCatletCommand, ConvergeVirtualCatletResult>
    {
        private readonly IHostInfoProvider _hostInfoProvider;

        public CreateVCatletCommandHandler(IPowershellEngine engine, ITaskMessaging messaging, ILogger log, IHostInfoProvider hostInfoProvider) : base(engine, messaging, log)
        {
            _hostInfoProvider = hostInfoProvider;
        }

        protected override EitherAsync<Error, ConvergeVirtualCatletResult> HandleCommand(CreateVCatletCommand command)
        {
            var config = command.Config;

            var hostSettings = HostSettingsBuilder.GetHostSettings();

            var planStorageSettings = Prelude.fun(() =>
                VMStorageSettings.Plan(hostSettings, LongToString(command.StorageId), config,
                    Option<VMStorageSettings>.None));

            var getParentConfig = Prelude.fun(() =>
                GetTemplate(hostSettings, config.Parent));

            var createVM = Prelude.fun((VMStorageSettings settings, 
                    Option<CatletConfig> parentConfig) =>
                CreateVM(settings, Engine, config, parentConfig));

            var createMetadata = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, Option<CatletConfig> parentConfig) =>
                    CreateMetadata(parentConfig, vmInfo, config, command.NewMachineId));

            return
                from plannedStorageSettings in planStorageSettings()
                from parentConfig in getParentConfig()
                from createdVM in createVM(plannedStorageSettings, parentConfig)
                from metadata in createMetadata(createdVM, parentConfig)
                from inventory in CreateMachineInventory(Engine, hostSettings, createdVM, _hostInfoProvider)
                select new ConvergeVirtualCatletResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata
                };
        }

        private static EitherAsync<Error, Option<CatletConfig>> GetTemplate(
            HostSettings hostSettings,
            string? parent)
        {
            if (string.IsNullOrWhiteSpace(parent))
                return Prelude.RightAsync<Error, Option<CatletConfig>> (
                    Option<CatletConfig>.None);

            return VirtualMachine.TemplateFromParents(hostSettings, parent).Map(Prelude.Some);
        }

        private static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> CreateVM(VMStorageSettings storageSettings, IPowershellEngine engine,
            CatletConfig config, Option<CatletConfig> parentConfig)
        {
            return (from storageIdentifier in storageSettings.StorageIdentifier.ToEitherAsync(Error.New(
                    "Unknown storage identifier, cannot create new virtual catlet"))
                let memoryConfig = config.Memory?.Startup
                                    ?? parentConfig.Map(x => x.Memory?.Startup ?? 1024).IfNone(1024)

                    from vm in VirtualMachine.Create(engine, config.Name, storageIdentifier,
                    storageSettings.VMPath, memoryConfig)
                select vm);
        }

        private EitherAsync<Error, VirtualCatletMetadata> CreateMetadata(
            Option<CatletConfig> optionalParentConfig,
            TypedPsObject<VirtualMachineInfo> vmInfo, CatletConfig config, Guid machineId)
        {
            return Prelude.RightAsync<Error, VirtualCatletMetadata>(
                    new VirtualCatletMetadata
                {
                    Id = Guid.NewGuid(),
                    MachineId = machineId,
                    VMId = vmInfo.Value.Id,
                    Fodder = config.Fodder,
                    Parent = config.Parent,
                    ParentConfig = optionalParentConfig.MatchUnsafe(
                        None: () => null, Some: c => c),

                })
                .Bind(metadata => SetMetadataId(vmInfo, metadata.Id).Map(_ => metadata));

        }
    }
}