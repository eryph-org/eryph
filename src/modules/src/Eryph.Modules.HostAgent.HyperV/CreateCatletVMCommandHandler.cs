using System;
using System.Reflection.PortableExecutable;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.HostAgent.Inventory;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.HostAgent
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

        protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(
            CreateCatletVMCommand command) =>
            from hostSettings in _hostSettingsProvider.GetHostSettings()
            from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            from plannedStorageSettings in VMStorageSettings.Plan(
                vmHostAgentConfig, LongToString(command.StorageId), command.Config, None)
            from createdVM in CreateVM(plannedStorageSettings, Engine, command.Config)
            let metadataId = Guid.NewGuid()
            from _ in SetMetadataId(createdVM, metadataId)
            let timestamp = DateTimeOffset.UtcNow
            from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, createdVM, _hostInfoProvider)
            select new ConvergeCatletResult
            {
                VmId = createdVM.Value.Id,
                MetadataId = metadataId,
                Inventory = inventory,
                Timestamp = timestamp,
            };

        private static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> CreateVM(
            VMStorageSettings storageSettings,
            IPowershellEngine engine,
            CatletConfig config) =>
            from storageIdentifier in storageSettings.StorageIdentifier
                .ToEitherAsync(Error.New(
                    "Cannot create catlet. The storage identifier is missing."))
            let startupMemory = config.Memory?.Startup ?? 1024
            from vm in VirtualMachine.Create(engine, config.Name, storageIdentifier,
                storageSettings.VMPath, startupMemory)
            select vm;
    }
}