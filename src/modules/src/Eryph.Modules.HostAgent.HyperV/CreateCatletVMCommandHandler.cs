using System;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.HostAgent;

[UsedImplicitly]
internal class CreateCatletVMCommandHandler(
    IPowershellEngine engine,
    ITaskMessaging messaging,
    ILogger log,
    IHostInfoProvider hostInfoProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    IPlacementConfigProvider placementConfigProvider)
    :
        CatletConfigCommandHandler<CreateCatletVMCommand, ConvergeCatletResult>(engine, messaging, log)
{
    protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(
        CreateCatletVMCommand command) =>
        from _placement in ValidatePlacement(command.Config)
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        from plannedStorageSettings in VMStorageSettings.FromCatletConfig(command.Config, vmHostAgentConfig)
        from createdVM in CreateVM(plannedStorageSettings, Engine, command.Config)
        from _ in SetMetadataId(createdVM, command.MetadataId)
        let timestamp = DateTimeOffset.UtcNow
        from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, createdVM, hostInfoProvider)
        select new ConvergeCatletResult
        {
            VmId = createdVM.Value.Id,
            Inventory = inventory,
            Timestamp = timestamp,
        };

    // The controller owns the datastore/environment vocabulary; reject placement on a
    // name it does not distribute (the agent's local path mapping is checked afterwards
    // by VMStorageSettings.FromCatletConfig). The default datastore/environment is always
    // allowed.
    private EitherAsync<Error, Unit> ValidatePlacement(CatletConfig config)
    {
        var placement = placementConfigProvider.Current;
        var dataStore = string.IsNullOrWhiteSpace(config.Store)
            ? EryphConstants.DefaultDataStoreName
            : config.Store;
        var environment = string.IsNullOrWhiteSpace(config.Environment)
            ? EryphConstants.DefaultEnvironmentName
            : config.Environment;

        if (!PlacementConfigValidation.IsDataStoreAllowed(placement, dataStore))
            return LeftAsync<Error, Unit>(Error.New(
                $"The data store '{dataStore}' is not part of the controller placement configuration."));

        if (!PlacementConfigValidation.IsEnvironmentAllowed(placement, environment))
            return LeftAsync<Error, Unit>(Error.New(
                $"The environment '{environment}' is not part of the controller placement configuration."));

        return RightAsync<Error, Unit>(unit);
    }

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
