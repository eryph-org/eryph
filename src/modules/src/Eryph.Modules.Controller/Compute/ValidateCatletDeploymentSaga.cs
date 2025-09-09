using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

/// <summary>
/// This saga is responsible for validating that the catlet
/// can be deployed to the selected host agent. This includes
/// checking the architecture, the configured networks and
/// ensuring that all disk genes are available and that the
/// datastore and environment exist.
/// </summary>
public class ValidateCatletDeploymentSaga(
    IBus bus,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<ValidateCatletDeploymentCommand, EryphSagaData<ValidateCatletDeploymentSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<PrepareGeneCommand>>
{
    protected override async Task Initiated(
        ValidateCatletDeploymentCommand message)
    {
        Data.Data.TenantId = message.TenantId;
        Data.Data.AgentName = message.AgentName;
        Data.Data.Config = message.Config;
        Data.Data.State = ValidateCatletDeploymentSagaState.Initiated;
        Data.Data.ResolvedGenes = message.ResolvedGenes;

        Data.Data.PendingGenes = message.ResolvedGenes
            .ToHashMap()
            .Filter(kvp => kvp.Key.GeneType is GeneType.Volume)
            .ToDictionary();

        if (Data.Data.PendingGenes.Count == 0)
        {
            await Complete();
            return;
        }

        foreach (var pendingGene in Data.Data.PendingGenes)
        {
            await StartNewTask(new PrepareGeneCommand
            {
                AgentName = Data.Data.AgentName,
                Id = pendingGene.Key,
                Hash = pendingGene.Value,
            });
        }
    }

    public Task Handle(OperationTaskStatusEvent<PrepareGeneCommand> message)
    {
        if (Data.Data.State >= ValidateCatletDeploymentSagaState.GenesPrepared)
            return Task.CompletedTask;

        return FailOrRun(message, async (PrepareGeneResponse response) =>
        {
            Data.Data.PendingGenes = Data.Data.PendingGenes
                .ToHashMap()
                .Remove(response.RequestedGene)
                .ToDictionary();

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = [response.Inventory],
                Timestamp = response.Timestamp,
            });

            if (Data.Data.PendingGenes.Count > 0)
                return;

            Data.Data.State = ValidateCatletDeploymentSagaState.GenesPrepared;
            await Complete();
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<ValidateCatletDeploymentSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<PrepareGeneCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
