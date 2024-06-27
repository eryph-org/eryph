using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Genes.Commands;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;
using Rebus.Sagas;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class BreedCatletSaga(IWorkflow workflowEngine)
    : OperationTaskWorkflowSaga<BreedCatletCommand, BreedCatletSagaData>(workflowEngine),
        IHandleMessages<OperationTaskStatusEvent<ResolveGeneSetsCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareParentGenomeCommand>>,
        IHandleMessages<OperationTaskStatusEvent<BreedCatletVMHostCommand>>
{
    public Task Handle(OperationTaskStatusEvent<PrepareParentGenomeCommand> message)
    {
        return FailOrRun<PrepareParentGenomeCommand, PrepareParentGenomeResponse>(message,
            (response) =>
            {
                // TODO Copied from CreateCatletSaga. Is Task.CompletedTask correct? Wouldn't this just cause the saga to never terminate?
                if (Data.CatletConfig == null || Data.CatletConfig.Parent != response.RequestedParent)
                    return Task.CompletedTask;

                Data.CatletConfig.Parent = response.ResolvedParent;

                return StartNewTask(new ResolveGeneSetsCommand()
                {
                    Config = Data.CatletConfig,
                    AgentName = Data.AgentName,
                }).AsTask();
            });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveGeneSetsCommand> message) =>
        FailOrRun(message, async () =>
        {
            await StartNewTask(new BreedCatletVMHostCommand()
            {
                AgentName = Data.AgentName,
                Config = Data.CatletConfig,
            });
        });

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

    protected override void CorrelateMessages(ICorrelationConfig<BreedCatletSagaData> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BreedCatletVMHostCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<PrepareParentGenomeCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveGeneSetsCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    protected override Task Initiated(BreedCatletCommand message)
    {
        Data.AgentName = message.AgentName;
        Data.CatletConfig = message.Config;

        var parentId = Optional(Data.CatletConfig.Parent).Filter(notEmpty);
        var command =  parentId.Match<IHostAgentCommand>(
            Some: pId => new PrepareParentGenomeCommand()
            {
                AgentName = Data.AgentName,
                ParentName = pId,
            },
            None: () => new ResolveGeneSetsCommand()
            {
                AgentName = Data.AgentName,
                Config = Data.CatletConfig,
            });

        return StartNewTask(command).AsTask();
    }
}
