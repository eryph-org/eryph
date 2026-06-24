using System;
using System.Collections.Generic;
using Eryph.Core.Genetics;
using System.Threading.Tasks;
using Dbosoft.Functional;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.CatletManagement;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

/// <summary>
/// This saga is responsible for resolving and expanding a
/// catlet specification.
/// </summary>
[UsedImplicitly]
internal class BuildCatletSpecificationSaga(
    IBus bus,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<BuildCatletSpecificationCommand, EryphSagaData<BuildCatletSpecificationSagaData>>(
            workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationGenePoolCommand>>
{
    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationGenePoolCommand> message)
    {
        if (Data.Data.State >= BuildCatletSpecificationSagaState.ConfigBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationGenePoolCommandResponse response) =>
        {
            Data.Data.State = BuildCatletSpecificationSagaState.ConfigBuilt;

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = response.Inventory,
                Timestamp = response.Timestamp,
            });

            Data.Data.ResolvedGenes = response.ResolvedGenes ?? new Dictionary<UniqueGeneIdentifier, GeneHash>();
            Data.Data.BuiltConfig = response.BuiltConfig;

            await Complete(new BuildCatletSpecificationCommandResponse
            {
                Architecture = Data.Data.Architecture,
                BuiltConfig = response.BuiltConfig,
                ResolvedGenes = response.ResolvedGenes,
            });
        });
    }

    protected override async Task Initiated(BuildCatletSpecificationCommand message)
    {
        Data.Data.State = BuildCatletSpecificationSagaState.Initiated;
        Data.Data.ContentType = message.ContentType;
        Data.Data.Configuration = message.Configuration;
        Data.Data.Architecture = message.Architecture;
        Data.Data.AgentName = message.AgentName;

        var contentType = Data.Data.ContentType ?? throw new System.InvalidOperationException("ContentType is required");
        var configuration = Data.Data.Configuration ?? throw new System.InvalidOperationException("Configuration is required");

        var parsedConfig = ParseCatletSpecificationConfig(
            contentType, configuration);
        if (parsedConfig.IsLeft)
        {
            await Fail(Error.Many(parsedConfig.LeftToSeq()).Print());
            return;
        }

        Data.Data.ParsedConfig = parsedConfig.ValueUnsafe();

        await StartNewTask(new BuildCatletSpecificationGenePoolCommand
        {
            AgentName = Data.Data.AgentName,
            CatletConfig = Data.Data.ParsedConfig,
            CatletArchitecture = Data.Data.Architecture,
        });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<BuildCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationGenePoolCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private static Either<Error, CatletConfig> ParseCatletSpecificationConfig(
        string contentType,
        string content) =>
        from parsedConfig in contentType switch
        {
            "application/json" => Try(() => CatletConfigJsonSerializer.Deserialize(content))
                .ToEither(ex => Error.New("The catlet configuration is invalid.", Error.New(ex))),
            "application/yaml" => Try(() => CatletConfigYamlSerializer.Deserialize(content))
                .ToEither(ex => Error.New("The catlet configuration is invalid.", Error.New(ex))),
            _ => Error.New($"The content type '{contentType}' is not supported."),
        }
        let validations = CatletConfigValidations.ValidateCatletConfig(parsedConfig)
                          | CatletSpecificationConfigValidator.Validate(parsedConfig)
        from _ in validations.ToEitherWithJsonPath(
            "The catlet configuration is invalid.",
            CatletConfigJsonSerializer.Options.PropertyNamingPolicy)
        select parsedConfig;
}
