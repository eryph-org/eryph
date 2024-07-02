using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Genes.Commands;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class BreedCatletSaga(IWorkflow workflowEngine)
    : OperationTaskWorkflowSaga<BreedCatletCommand, BreedCatletSagaData>(workflowEngine),
        IHandleMessages<OperationTaskStatusEvent<ResolveGenesCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletParentCommand>>
{
    protected override void CorrelateMessages(ICorrelationConfig<BreedCatletSagaData> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BreedCatletVMHostCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        /*
        config.Correlate<OperationTaskStatusEvent<PrepareParentGenomeCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveGeneSetsCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        */
        config.Correlate<OperationTaskStatusEvent<ResolveGenesCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveCatletParentCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    protected override Task Initiated(BreedCatletCommand message)
    {
        Data.AgentName = message.AgentName;
        Data.CatletConfig = message.Config;

        return StartNewTask(new ResolveGenesCommand()
        {
            Config = Data.CatletConfig,
            AgentName = Data.AgentName,
        }).AsTask();
    }

    /*
    public Task Handle(OperationTaskStatusEvent<PrepareParentGenomeCommand> message)
    {
        return FailOrRun(message, (PrepareParentGenomeResponse response) =>
        {
            // TODO Copied from CreateCatletSaga. Is Task.CompletedTask correct? Wouldn't this just cause the saga to never terminate?
            if (Data.CatletConfig == null || Data.CatletConfig.Parent != response.RequestedParent)
                return Task.CompletedTask;

            Data.CatletConfig.Parent = response.ResolvedParent;

            return StartNewTask(new ResolveGenesCommand()
            {
                Config = Data.CatletConfig,
                AgentName = Data.AgentName,
            }).AsTask();
        });
    }
    */

    /*
    public Task Handle(OperationTaskStatusEvent<ResolveGeneSetsCommand> message) =>
        FailOrRun(message, async () =>
        {
            await StartNewTask(new BreedCatletVMHostCommand()
            {
                AgentName = Data.AgentName,
                Config = Data.CatletConfig,
            });
        });
    */

    public Task Handle(OperationTaskStatusEvent<BreedCatletVMHostCommand> message) =>
        FailOrRun(message, async (BreedCatletVMHostCommandResponse response) =>
        {
            Data.BreedConfig = response.BreedConfig;
            Data.ParentConfig = response.ParentConfig;
            await Complete(new BreedCatletCommandResponse()
            {
                BreedConfig = Data.BreedConfig,
                ParentConfig = Data.ParentConfig,
            });
        });

    // TODO PrepareParentGenes
    // TODO PrepareGeneSets
    // TODO Breed parent config
    // TODO Breed child config


    public Task Handle(OperationTaskStatusEvent<ResolveGenesCommand> message) =>
        FailOrRun(message, async (ResolveGenesCommandResponse response) =>
        {
            Data.ResolvedConfig = response.Config;
            if (notEmpty(Data.ResolvedConfig.Parent))
            {
                await StartNewTask(new ResolveCatletParentCommand()
                {
                    AgentName = Data.AgentName,
                    ParentId = Data.ResolvedConfig.Parent,
                });
                return;
            }

            await Complete(new BreedCatletCommandResponse()
            {
                BreedConfig = Data.BreedConfig,
                ParentConfig = null,
            });
        });

    public Task Handle(OperationTaskStatusEvent<ResolveCatletParentCommand> message) =>
        FailOrRun(message, async (ResolveCatletParentCommandResponse response) =>
        {
            if (Data.ResolvedParents.ContainsKey(response.ParentId))
                return;

            Data.ResolvedParents.Add(response.ParentId, response.Config);

            if (notEmpty(response.Config.Parent))
            {
                await StartNewTask(new ResolveCatletParentCommand()
                {
                    AgentName = Data.AgentName,
                    ParentId = response.Config.Parent,
                });
                return;
            }


            if (Data.ResolvedConfig is null)
                throw new InvalidOperationException();

            if (Data.ResolvedConfig.Parent is null)
                throw new InvalidOperationException();

            if (!Data.ResolvedParents.TryGetValue(Data.ResolvedConfig.Parent, out var parentConfig))
                throw new InvalidOperationException();

            var resolvedParents = Data.ResolvedParents.ToHashMap();

            var bredParentConfig = BreedRecursively(parentConfig, resolvedParents)
                .IfLeft(e => e.ToException().Rethrow<CatletConfig>());

            var bredConfig = CatletBreeding.Breed(bredParentConfig, Data.ResolvedConfig)
                .IfLeft(e => e.ToException().Rethrow<CatletConfig>());

            await Complete(new BreedCatletCommandResponse()
            {
                BreedConfig = bredConfig,
                ParentConfig = bredParentConfig,
            });
        });

    // TODO limit recursion
    private static Either<Error, CatletConfig> BreedRecursively(
        CatletConfig catletConfig,
        HashMap<string, CatletConfig> parents) =>
        Optional(catletConfig.Parent).Filter(notEmpty).Match(
                Some: parentId =>
                    from parentConfig in parents.Find(parentId)
                        .ToEither(Error.New($"Could not find parent {parentId}"))
                    from bredParentConfig in BreedRecursively(parentConfig, parents)
                    let bredConfig = bredParentConfig.Breed(catletConfig, parentId)
                    select bredConfig,
                None: () => catletConfig);
}
