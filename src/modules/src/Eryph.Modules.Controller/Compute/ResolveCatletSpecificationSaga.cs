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
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;


/// <summary>
/// This saga is responsible for resolving and expanding a
/// catlet specification.
/// </summary>
internal class ResolveCatletSpecificationSaga(
    IWorkflow workflow,
    IStorageManagementAgentLocator agentLocator,
    IBus bus)
    : OperationTaskWorkflowSaga<ResolveCatletSpecificationCommand, EryphSagaData<ResolveCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveGenesCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareGeneCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ExpandFodderVMCommand>>
{
    protected override async Task Initiated(ResolveCatletSpecificationCommand message)
    {
        Data.Data.State = ResolveCatletSpecificationSagaState.Initiated;
        Data.Data.ConfigYaml = message.ConfigYaml;
        Data.Data.Architecture = message.Architecture;
        Data.Data.AgentName = agentLocator.FindAgentForGenePool();

        var parsedConfig = ParseCatletConfigYaml(message.ConfigYaml);
        if (parsedConfig.IsLeft)
        {
            await Fail(Error.Many(parsedConfig.LeftToSeq()).Print());
            return;
        }

        Data.Data.Config = parsedConfig.ValueUnsafe();

        await StartNewTask(new ResolveCatletConfigCommand()
        {
            AgentName = Data.Data.AgentName,
            Config = Data.Data.Config,
        });
    }

    private static Either<Error, CatletConfig> ParseCatletConfigYaml(string yaml) =>
        // TODO improve error mapping / consider JSON path
        from parsedConfig in Try(() => CatletConfigYamlSerializer.Deserialize(yaml))
            .ToEither(ex => Error.New("The catlet configuration is invalid.", Error.New(ex)))
        from _ in CatletConfigValidations.ValidateCatletConfig(parsedConfig)
            .MapFail(i => i.ToError())
            .ToEither()
            .MapLeft(errors => Error.New("The catlet configuration is invalid.", Error.Many(errors)))
        select parsedConfig;

    public Task Handle(OperationTaskStatusEvent<ResolveCatletConfigCommand> message)
    {
        if (Data.Data.State >= ResolveCatletSpecificationSagaState.Resolved)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveCatletConfigCommandResponse response) =>
        {
            if (Data.Data.Config is null)
                throw new InvalidOperationException("Config is missing.");

            Data.Data.State = ResolveCatletSpecificationSagaState.Resolved;

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = response.Inventory,
                Timestamp = response.Timestamp,
            });

            var result = PrepareConfigs(
                Data.Data.Config,
                response.ResolvedGeneSets.ToHashMap(),
                response.ParentConfigs.ToHashMap());
            if (result.IsLeft)
            {
                await Fail(Error.Many(result.LeftToSeq()).Print());
                return;
            }

            Data.Data.Config = result.ValueUnsafe().Config;
            Data.Data.ExpandedConfig = result.ValueUnsafe().BredConfig;
            Data.Data.ParentConfig = result.ValueUnsafe().ParentConfig.IfNoneUnsafe((CatletConfig?)null);

            var geneIds = CatletGeneCollecting.CollectGenes(Data.Data.ExpandedConfig);
            if (geneIds.IsFail)
            {
                await Fail(Error.Many(geneIds.FailToSeq()).Print());
                return;
            }

            await StartNewTask(new ResolveGenesCommand
            {
                AgentName = Data.Data.AgentName,
                CatletArchitecture = Data.Data.Architecture,
                Genes = geneIds.SuccessToSeq().Flatten()
                    .Filter(g => g.GeneType is GeneType.Fodder or GeneType.Volume)
                    // Skip genes which have parent catlet as the informational source
                    .Filter(g => g.GeneIdentifier.GeneName != GeneName.New("catlet"))
                    .ToList(),
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveGenesCommand> message)
    {
        throw new NotImplementedException();
    }

    public Task Handle(OperationTaskStatusEvent<PrepareGeneCommand> message)
    {
        throw new NotImplementedException();
    }

    public Task Handle(OperationTaskStatusEvent<ExpandFodderVMCommand> message)
    {
        throw new NotImplementedException();
    }

    private static Either<Error, (CatletConfig Config, CatletConfig BredConfig, Option<CatletConfig> ParentConfig)> PrepareConfigs(
        CatletConfig config,
        HashMap<GeneSetIdentifier, GeneSetIdentifier> resolvedGeneSets,
        HashMap<GeneSetIdentifier, CatletConfig> parentConfigs) =>
        from resolvedConfig in CatletGeneResolving.ResolveGeneSetIdentifiers(config, resolvedGeneSets)
            .MapLeft(e => Error.New("Could not resolve genes in the catlet config.", e))
        from breedingResult in CatletPedigree.Breed(config, resolvedGeneSets, parentConfigs)
            .MapLeft(e => Error.New("Could not breed the catlet.", e))
        let bredConfigWithDefaults = CatletConfigDefaults.ApplyDefaults(breedingResult.Config)
        select (resolvedConfig, bredConfigWithDefaults, breedingResult.ParentConfig);
}
