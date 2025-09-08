using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class ExpandNewCatletConfigSaga(
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<ExpandNewCatletConfigCommand, EryphSagaData<ExpandNewCatletConfigSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    protected override async Task Initiated(ExpandNewCatletConfigCommand message)
    {
        Data.Data.State = ExpandNewCatletConfigSagaState.Initiated;
        Data.Data.Config = message.Config;
        Data.Data.ShowSecrets = message.ShowSecrets;

        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Architecture = Architecture.New(EryphConstants.DefaultArchitecture);

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ConfigYaml = CatletConfigYamlSerializer.Serialize(message.Config),
            Architecture = Data.Data.Architecture,
        });
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            var redactedConfig = Data.Data.ShowSecrets
                ? response.BuiltConfig
                : CatletConfigRedactor.RedactSecrets(response.BuiltConfig);

            await Complete(new ExpandNewCatletConfigCommandResponse
            {
                Config = CatletConfigNormalizer.Minimize(redactedConfig),
            });
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<ExpandNewCatletConfigSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
