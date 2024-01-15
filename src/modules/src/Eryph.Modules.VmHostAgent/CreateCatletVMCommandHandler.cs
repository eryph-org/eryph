using System;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.VmAgent;
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
    internal class CreateCatletVMCommandHandler : 
        CatletConfigCommandHandler<CreateCatletVMCommand, ConvergeCatletResult>
    {
        private readonly IHostInfoProvider _hostInfoProvider;
        private readonly IHostSettingsProvider _hostSettingsProvider;
        private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

        public CreateCatletVMCommandHandler(
            IPowershellEngine engine,
            ITaskMessaging messaging,
            ILogger log,
            IHostInfoProvider hostInfoProvider,
            IHostSettingsProvider hostSettingsProvider,
            IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
            : base(engine, messaging, log)
        {
            _hostInfoProvider = hostInfoProvider;
            _hostSettingsProvider = hostSettingsProvider;
            _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
        }

        protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(CreateCatletVMCommand command)
        {
            var config = command.Config;

            var getParentConfig = Prelude.fun((VmHostAgentConfiguration vmHostAgentConfig) =>
                GetTemplate(vmHostAgentConfig, config.Parent));

            var createVM = Prelude.fun((VMStorageSettings settings, 
                    Option<CatletConfig> parentConfig) =>
                CreateVM(settings, Engine, config, parentConfig));

            var createMetadata = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, Option<CatletConfig> parentConfig) =>
                    CreateMetadata(parentConfig, vmInfo, config, command.NewMachineId));

            return
                from hostSettings in _hostSettingsProvider.GetHostSettings()
                from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
                from plannedStorageSettings in VMStorageSettings.Plan(vmHostAgentConfig, LongToString(command.StorageId), config,
                    Option<VMStorageSettings>.None)
                from parentConfig in getParentConfig(vmHostAgentConfig)
                from createdVM in createVM(plannedStorageSettings, parentConfig)
                from metadata in createMetadata(createdVM, parentConfig)
                from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, createdVM, _hostInfoProvider)
                select new ConvergeCatletResult
                {
                    Inventory = inventory,
                    MachineMetadata = metadata
                };
        }

        private static EitherAsync<Error, Option<CatletConfig>> GetTemplate(
            VmHostAgentConfiguration vmHostAgentConfig,
            string? parent)
        {
            if (string.IsNullOrWhiteSpace(parent))
                return Prelude.RightAsync<Error, Option<CatletConfig>> (
                    Option<CatletConfig>.None);

            return VirtualMachine.TemplateFromParents(vmHostAgentConfig, parent).Map(Prelude.Some);
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

        private EitherAsync<Error, CatletMetadata> CreateMetadata(
            Option<CatletConfig> optionalParentConfig,
            TypedPsObject<VirtualMachineInfo> vmInfo, CatletConfig config, Guid machineId)
        {
            return Prelude.RightAsync<Error, CatletMetadata>(
                    new CatletMetadata
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