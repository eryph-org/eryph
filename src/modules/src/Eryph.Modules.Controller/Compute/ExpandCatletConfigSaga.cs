﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class ExpandCatletConfigSaga(
    IWorkflow workflow,
    IVirtualMachineDataService vmDataService,
    IVirtualMachineMetadataService metadataService)
    : OperationTaskWorkflowSaga<ExpandCatletConfigCommand, EryphSagaData<ExpandCatletConfigSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<PrepareCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ExpandFodderVMCommand>>
{
    protected override async Task Initiated(ExpandCatletConfigCommand message)
    {
        Data.Data.State = ExpandCatletConfigSagaState.Initiated;
        Data.Data.CatletId = message.CatletId;
        Data.Data.Config = message.Config;
        Data.Data.ShowSecrets = message.ShowSecrets;

        if (Data.Data.CatletId == Guid.Empty)
        {
            await Fail("Config for existing catlet cannot be expanded because the catlet Id is missing.");
            return;
        }

        var machineInfo = await vmDataService.GetVM(Data.Data.CatletId)
            .Map(m => m.IfNoneUnsafe((Catlet?)null));
        if (machineInfo is null)
        {
            await Fail($"Config for existing catlet cannot be expanded because the catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        Data.Data.AgentName = machineInfo.AgentName;

        var metadata = await metadataService.GetMetadata(machineInfo.MetadataId)
            .Map(m => m.IfNoneUnsafe((CatletMetadata?)null));
        if (metadata is null)
        {
            await Fail($"Config for existing catlet cannot be expanded because the metadata for catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        await StartNewTask(new PrepareCatletConfigCommand
        {
            CatletId = message.CatletId,
            Config = message.Config,
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareCatletConfigCommand> message)
    {
        if (Data.Data.State >= ExpandCatletConfigSagaState.ConfigPrepared)
            return Task.CompletedTask;

        return FailOrRun(message, async (PrepareCatletConfigCommandResponse response) =>
        {
            Data.Data.State = ExpandCatletConfigSagaState.ConfigPrepared;
            Data.Data.Config = response.ResolvedConfig;
            Data.Data.BredConfig = response.BredConfig;
            Data.Data.ResolvedGenes = response.ResolvedGenes;

            var metadata = await GetCatletMetadata(Data.Data.CatletId);
            if (metadata.IsNone)
            {
                await Fail($"The metadata for catlet {Data.Data.CatletId} was not found.");
                return;
            }

            await StartNewTask(new ExpandFodderVMCommand
            {
                AgentName = Data.Data.AgentName,
                CatletMetadata = metadata.ValueUnsafe().Metadata,
                Config = Data.Data.BredConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ExpandFodderVMCommand> message)
    {
        return FailOrRun(message, async (ExpandFodderVMCommandResponse response) =>
        {
            var redactedConfig = Data.Data.ShowSecrets
                ? response.Config
                : CatletConfigRedactor.RedactSecrets(response.Config);

            await Complete(new ExpandCatletConfigCommandResponse
            {
                Config = CatletConfigNormalizer.Minimize(redactedConfig),
            });
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<ExpandCatletConfigSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<PrepareCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ExpandFodderVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private Task<Option<(Catlet Catlet, CatletMetadata Metadata)>> GetCatletMetadata(Guid catletId) =>
        from catlet in vmDataService.GetVM(catletId)
        from metadata in metadataService.GetMetadata(catlet.MetadataId)
        select (catlet, metadata);
}
