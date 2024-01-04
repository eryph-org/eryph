using System;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
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
        private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

        public CreateCatletVMCommandHandler(
            IPowershellEngine engine,
            ITaskMessaging messaging,
            ILogger log,
            IHostInfoProvider hostInfoProvider,
            IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
            : base(engine, messaging, log)
        {
            _hostInfoProvider = hostInfoProvider;
            _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
        }

        protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(CreateCatletVMCommand command)
        {
            var config = command.Config;

            var hostSettings = HostSettingsBuilder.GetHostSettings();

            var getParentConfig = Prelude.fun(() =>
                GetTemplate(hostSettings, config.Parent));

            var createVM = Prelude.fun((VMStorageSettings settings, 
                    Option<CatletConfig> parentConfig) =>
                CreateVM(settings, Engine, config, parentConfig));

            var createMetadata = Prelude.fun(
                (TypedPsObject<VirtualMachineInfo> vmInfo, Option<CatletConfig> parentConfig) =>
                    CreateMetadata(parentConfig, vmInfo, config, command.NewMachineId));

            return
                from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration()
                from plannedStorageSettings in VMStorageSettings.Plan(vmHostAgentConfig, hostSettings, LongToString(command.StorageId), config,
                    Option<VMStorageSettings>.None)
                from parentConfig in getParentConfig()
                from createdVM in createVM(plannedStorageSettings, parentConfig)
                from metadata in createMetadata(createdVM, parentConfig)
                from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, hostSettings, createdVM, _hostInfoProvider)
                select new ConvergeCatletResult
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