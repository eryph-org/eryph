using Dbosoft.Functional;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Yaml;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Pipes;
using LanguageExt.UnsafeValueAccess;
using Rebus.Bus;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Messages.Resources.CatletSpecifications;
using Rebus.Sagas;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;


/// <summary>
/// This saga is responsible for resolving and expanding a
/// catlet specification.
/// </summary>
internal class ResolveCatletSpecificationSaga(
    IBus bus,
    IWorkflow workflow,
    IStorageManagementAgentLocator agentLocator)
    : OperationTaskWorkflowSaga<ResolveCatletSpecificationCommand, EryphSagaData<ResolveCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationGenePoolCommand>>
{
    protected override async Task Initiated(ResolveCatletSpecificationCommand message)
    {
        Data.Data.State = ResolveCatletSpecificationSagaState.Initiated;
        Data.Data.ConfigYaml = message.ConfigYaml;
        Data.Data.Architecture = message.Architecture;
        Data.Data.AgentName = message.AgentName;

        var parsedConfig = ParseCatletConfigYaml(message.ConfigYaml);
        if (parsedConfig.IsLeft)
        {
            await Fail(Error.Many(parsedConfig.LeftToSeq()).Print());
            return;
        }

        Data.Data.Config = parsedConfig.ValueUnsafe();

        await StartNewTask(new BuildCatletSpecificationGenePoolCommand()
        {
            AgentName = Data.Data.AgentName,
            CatletConfig = Data.Data.Config,
            CatletArchitecture = Data.Data.Architecture,
        });
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationGenePoolCommand> message)
    {
        if (Data.Data.State >= ResolveCatletSpecificationSagaState.ConfigBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationGenePoolCommandResponse response) =>
        {
            Data.Data.State = ResolveCatletSpecificationSagaState.ConfigBuilt;

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = response.Inventory,
                Timestamp = response.Timestamp,
            });

            Data.Data.ResolvedGenes = response.ResolvedGenes;
            Data.Data.BuiltConfig = response.BuiltConfig;

            await Complete(new ResolveCatletSpecificationCommandResponse
            {
                BuiltConfig = response.BuiltConfig,
                ResolvedGenes = response.ResolvedGenes,
            });
        });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<ResolveCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationGenePoolCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private static Either<Error, CatletConfig> ParseCatletConfigYaml(string yaml) =>
        // TODO improve error mapping / consider JSON path
        // TODO normalize config?
        from parsedConfig in Try(() => CatletConfigYamlSerializer.Deserialize(yaml))
            .ToEither(ex => Error.New("The catlet configuration is invalid.", Error.New(ex)))
        from _ in CatletConfigValidations.ValidateCatletConfig(parsedConfig)
            .MapFail(i => i.ToError())
            .ToEither()
            .MapLeft(errors => Error.New("The catlet configuration is invalid.", Error.Many(errors)))
        select parsedConfig;
}
