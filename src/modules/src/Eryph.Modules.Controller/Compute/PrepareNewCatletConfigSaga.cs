using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

internal class PrepareNewCatletConfigSaga(
    IWorkflow workflow,
    IBus bus)
    : OperationTaskWorkflowSaga<PrepareNewCatletConfigCommand, EryphSagaData<PrepareNewCatletConfigSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PlaceCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveGenesCommand>>
{
    protected override async Task Initiated(PrepareNewCatletConfigCommand message)
    {
        Data.Data.Config = message.Config;
        Data.Data.State = PrepareNewCatletConfigSagaState.Initiated;

        await StartNewTask(new ValidateCatletConfigCommand
        {
            Config = message.Config,
        });
    }

    public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
    {
        if (Data.Data.State >= PrepareNewCatletConfigSagaState.ConfigValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async (ValidateCatletConfigCommandResponse response) =>
        {
            Data.Data.Config = response.Config;
            Data.Data.State = PrepareNewCatletConfigSagaState.ConfigValidated;

            await StartNewTask(new PlaceCatletCommand
            {
                Config = Data.Data.Config
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<PlaceCatletCommand> message)
    {
        if (Data.Data.State >= PrepareNewCatletConfigSagaState.Placed)
            return Task.CompletedTask;

        return FailOrRun(message, async (PlaceCatletResult response) =>
        {
            Data.Data.State = PrepareNewCatletConfigSagaState.Placed;
            Data.Data.AgentName = response.AgentName;
            Data.Data.Architecture = response.Architecture;

            await StartNewTask(new ResolveCatletConfigCommand()
            {
                AgentName = response.AgentName,
                Config = Data.Data.Config,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveCatletConfigCommand> message)
    {
        if (Data.Data.State >= PrepareNewCatletConfigSagaState.Resolved)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveCatletConfigCommandResponse response) =>
        {
            if (Data.Data.Config is null)
                throw new InvalidOperationException("Config is missing.");

            Data.Data.State = PrepareNewCatletConfigSagaState.Resolved;

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
                await Fail(ErrorUtils.PrintError(Error.Many(result.LeftToSeq())));
                return;
            }

            Data.Data.Config = result.ValueUnsafe().Config;
            Data.Data.BredConfig = result.ValueUnsafe().BredConfig;
            Data.Data.ParentConfig = result.ValueUnsafe().ParentConfig.IfNoneUnsafe((CatletConfig?)null);

            var geneIds = CatletGeneCollecting.CollectGenes(Data.Data.BredConfig);
            if (geneIds.IsFail)
            {
                await Fail(ErrorUtils.PrintError(Error.Many(geneIds.FailToSeq())));
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
        if (Data.Data.State >= PrepareNewCatletConfigSagaState.GenesResolved)
            return Task.CompletedTask;

        return FailOrRun(message, (ResolveGenesCommandResponse response) =>
        {
            if (Data.Data.Config is null)
                throw new InvalidOperationException("Config is missing.");

            Data.Data.State = PrepareNewCatletConfigSagaState.GenesResolved;
            Data.Data.ResolvedGenes = response.ResolvedGenes;

            return Complete(new PrepareNewCatletConfigCommandResponse
            {
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                ParentConfig = Data.Data.ParentConfig,
                BredConfig = Data.Data.BredConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<PrepareNewCatletConfigSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<PlaceCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveGenesCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
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
