using System;
using System.Threading.Tasks;
using Dbosoft.Functional;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
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
            ContentType = "application/yaml",
            Configuration = CatletConfigYamlSerializer.Serialize(message.Config),
            Architecture = Data.Data.Architecture,
        });
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= ExpandNewCatletConfigSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = ExpandNewCatletConfigSagaState.SpecificationBuilt;

            var configWithSystemVariables = CatletSystemDataFeeding.FeedSystemVariables(
                response.BuiltConfig, "#catletId", "#vmId");

            var substitutionResult = CatletConfigVariableSubstitutions
                .SubstituteVariables(configWithSystemVariables)
                .ToEitherWithJsonPath(
                    "The variables in the catlet config cannot be substituted.",
                    CatletConfigJsonSerializer.Options.PropertyNamingPolicy);
            if (substitutionResult.IsLeft)
            {
                await Fail(Error.Many(substitutionResult.LeftToSeq()).Print());
                return;
            }

            var redactedConfig = Data.Data.ShowSecrets
                ? substitutionResult.ValueUnsafe()
                : CatletConfigRedactor.RedactSecrets(substitutionResult.ValueUnsafe());

            await Complete(new ExpandNewCatletConfigCommandResponse
            {
                Config = CatletConfigNormalizer.Trim(redactedConfig),
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
