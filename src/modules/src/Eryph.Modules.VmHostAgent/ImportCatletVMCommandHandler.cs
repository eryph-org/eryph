﻿using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.VmAgent;
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

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class ImportCatletVMCommandHandler :
    CatletConfigCommandHandler<ImportCatletVMCommand, ConvergeCatletResult>
{
    private readonly IHostInfoProvider _hostInfoProvider;
    private readonly IHostSettingsProvider _hostSettingsProvider;
    private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

    public ImportCatletVMCommandHandler(
        IPowershellEngine engine,
        ITaskMessaging messaging,
        ILogger log,
        IHostInfoProvider hostInfoProvider,
        IHostSettingsProvider hostSettingsProvider,
        IVmHostAgentConfigurationManager vmHostAgentConfigurationManager) : base(engine, messaging, log)
    {
        _hostInfoProvider = hostInfoProvider;
        _hostSettingsProvider = hostSettingsProvider;
        _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
    }

    protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(ImportCatletVMCommand command)
    {
        var config = command.Config;

        var planStorageSettings = Prelude.fun((VmHostAgentConfiguration vmHostAgentConfig) =>
            VMStorageSettings.Plan(vmHostAgentConfig, LongToString(command.StorageId), config,
                Option<VMStorageSettings>.None));

        var getTemplate = Prelude.fun(() =>
            VirtualMachine.VMTemplateFromPath(Engine, command.Path));

        var importVM = Prelude.fun((VMStorageSettings settings, TypedPsObject<PlannedVirtualMachineInfo> template) =>
            ImportVM(config, settings, Engine, template));

        var createMetadata = Prelude.fun(
            (TypedPsObject<VirtualMachineInfo> vmInfo, Option<TypedPsObject<PlannedVirtualMachineInfo>> plannedVM) =>
                CreateMetadata(Engine, plannedVM, vmInfo, config, command.NewMachineId));

        return
            from hostSettings in _hostSettingsProvider.GetHostSettings()
            from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            from plannedStorageSettings in planStorageSettings(vmHostAgentConfig)
            from template in getTemplate()
            from importedVM in importVM(plannedStorageSettings, template)
            from metadata in createMetadata(importedVM, template)
            from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, importedVM, _hostInfoProvider)
            select new ConvergeCatletResult
            {
                Inventory = inventory,
                MachineMetadata = metadata,
                Timestamp = DateTimeOffset.UtcNow,
            };
    }

    private static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ImportVM(CatletConfig config, 
        VMStorageSettings storageSettings, IPowershellEngine engine,
        TypedPsObject<PlannedVirtualMachineInfo> template)
    {
        return (from storageIdentifier in storageSettings.StorageIdentifier.ToEitherAsync(Error.New(
                "Unknown storage identifier, cannot create new virtual catlet"))

            from vm in
                VirtualMachine.ImportTemplate(engine, config.Name,
                    storageIdentifier,
                    storageSettings,
                    template)
            select vm);
    }

    private EitherAsync<Error, CatletMetadata> CreateMetadata(
        IPowershellEngine engine,
        Option<TypedPsObject<PlannedVirtualMachineInfo>> optionalTemplate,
        TypedPsObject<VirtualMachineInfo> vmInfo, CatletConfig config, Guid machineId)
    {
        return Prelude.RightAsync<Error, CatletMetadata>(CreateMetadataAsync())
            .Bind(metadata => SetMetadataId(vmInfo, metadata.Id).Map(_ => metadata));

        async Task<CatletMetadata> CreateMetadataAsync()
        {
            return new CatletMetadata
            {
                Id = Guid.NewGuid(),
                MachineId = machineId,
                VMId = vmInfo.Value.Id,
                Fodder = config.Fodder,
                Variables = config.Variables,
                ParentConfig = await optionalTemplate.MatchUnsafe(
                    None: () => null, Some: async t => await t.ToVmConfig(engine))!
            };

        }
    }
}