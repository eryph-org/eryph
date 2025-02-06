using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
public class ExpandNewCatletConfigSaga(
    IBus bus,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<ExpandNewCatletConfigCommand, EryphSagaData<ExpandNewCatletConfigSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<PrepareNewCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareGeneCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ExpandFodderVMCommand>>
{
    protected override async Task Initiated(ExpandNewCatletConfigCommand message)
    {
        Data.Data.State = ExpandNewCatletConfigSagaState.Initiated;
        Data.Data.Config = message.Config;

        await StartNewTask(new PrepareNewCatletConfigCommand
        {
            Config = message.Config,
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareNewCatletConfigCommand> message)
    {
        if (Data.Data.State >= ExpandNewCatletConfigSagaState.ConfigPrepared)
            return Task.CompletedTask;

        return FailOrRun(message, async (PrepareNewCatletConfigCommandResponse response) =>
        {
            Data.Data.State = ExpandNewCatletConfigSagaState.ConfigPrepared;

            Data.Data.AgentName = response.AgentName;

            Data.Data.Config = response.Config;
            Data.Data.BredConfig = response.BredConfig;

            Data.Data.ResolvedGenes = response.ResolvedGenes;

            Data.Data.PendingGenes = response.ResolvedGenes
                .Filter(g => g.GeneType == GeneType.Fodder)
                .ToList();

            if (Data.Data.PendingGenes.Count == 0)
            {
                await StartExpandFodder();
                return;
            }

            var commands = Data.Data.PendingGenes.Map(id => new PrepareGeneCommand
            {
                AgentName = Data.Data.AgentName,
                Gene = id,
            });

            foreach (var command in commands)
            {
                await StartNewTask(command);
            }
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareGeneCommand> message)
    {
        if (Data.Data.State >= ExpandNewCatletConfigSagaState.GenesPrepared)
            return Task.CompletedTask;

        return FailOrRun(message, async (PrepareGeneResponse response) =>
        {
            Data.Data.PendingGenes = Data.Data.PendingGenes
                .Except([response.RequestedGene])
                .ToList();

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = [response.Inventory],
                Timestamp = response.Timestamp,
            });

            if (Data.Data.PendingGenes.Count > 0)
                return;

            await StartExpandFodder();
        });
    }

    public Task Handle(OperationTaskStatusEvent<ExpandFodderVMCommand> message)
    {
        return FailOrRun(message, async (ExpandFodderVMCommandResponse response) =>
        {
            await Complete(new ExpandNewCatletConfigCommandResponse
            {
                Config = response.Config,
            });
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<ExpandNewCatletConfigSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<PrepareNewCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<PrepareGeneCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ExpandFodderVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private async Task StartExpandFodder()
    {
        Data.Data.State = ExpandNewCatletConfigSagaState.GenesPrepared;

        await StartNewTask(new ExpandFodderVMCommand
        {
            AgentName = Data.Data.AgentName,
            Config = Data.Data.BredConfig,
            ResolvedGenes = Data.Data.ResolvedGenes,
        });
    }
}
