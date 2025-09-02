using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Yaml;
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Resources.Machines;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class ExpandCatletConfigSaga(
    IWorkflow workflow,
    ICatletDataService vmDataService,
    ICatletMetadataService metadataService)
    : OperationTaskWorkflowSaga<ExpandCatletConfigCommand, EryphSagaData<ExpandCatletConfigSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletSpecificationCommand>>
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

        var machineInfo = await vmDataService.Get(Data.Data.CatletId)
            .Map(m => m.IfNoneUnsafe((Catlet?)null));
        if (machineInfo is null)
        {
            await Fail($"Config for existing catlet cannot be expanded because the catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        Data.Data.AgentName = machineInfo.AgentName;

        var metadata = await metadataService.GetMetadata(machineInfo.MetadataId);
        if (metadata is null)
        {
            await Fail($"Config for existing catlet cannot be expanded because the metadata for catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        if (metadata.IsDeprecated || metadata.Metadata is null)
        {
            await Fail($"Config for existing catlet cannot be expanded because the catlet {Data.Data.CatletId} has been created with an old version of eryph.");
            return;
        }

        Data.Data.Architecture = metadata.Metadata.Architecture;

        await StartNewTask(new ResolveCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            Architecture = Data.Data.Architecture,
            ConfigYaml = CatletConfigYamlSerializer.Serialize(message.Config),
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveCatletSpecificationCommand> message)
    {
        return FailOrRun(message, async (ResolveCatletSpecificationCommandResponse response) =>
        {
            // TODO merge with existing config

            var redactedConfig = Data.Data.ShowSecrets
                ? response.BuiltConfig
                : CatletConfigRedactor.RedactSecrets(response.BuiltConfig);

            await Complete(new ExpandCatletConfigCommandResponse
            {
                Config = CatletConfigNormalizer.Minimize(redactedConfig),
            });
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<ExpandCatletConfigSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<ResolveCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
