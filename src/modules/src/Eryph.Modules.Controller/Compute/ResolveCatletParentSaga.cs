using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class ResolveCatletParentSaga(IWorkflow workflowEngine)
    : OperationTaskWorkflowSaga<ResolveCatletParentCommand, ResolveCatletParentSagaData>(workflowEngine),
        IHandleMessages<OperationTaskStatusEvent<EnsureParentVMHostCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveGenesCommand>>
{
    protected override void CorrelateMessages(
        ICorrelationConfig<ResolveCatletParentSagaData> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<EnsureParentVMHostCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveGenesCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    protected override async Task Initiated(ResolveCatletParentCommand message)
    {
        Data.AgentName = message.AgentName;
        Data.ParentId = message.ParentId;

        await StartNewTask(new EnsureParentVMHostCommand()
        {
            AgentName = Data.AgentName,
            ParentId = Data.ParentId,
        });
    }

    public Task Handle(OperationTaskStatusEvent<EnsureParentVMHostCommand> message)
    {
        if (Data.State >= ResolveCatletParentSagaState.CatletGeneFetched)
            return Task.CompletedTask;

        return FailOrRun(message, async (EnsureParentVMHostCommandResponse response) =>
        {
            Data.State = ResolveCatletParentSagaState.CatletGeneFetched;
            Data.ParentConfig = response.Config;

            await StartNewTask(new ResolveGenesCommand()
            {
                AgentName = Data.AgentName,
                Config = Data.ParentConfig,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveGenesCommand> message)
    {
        return FailOrRun(message, async (ResolveGenesCommandResponse response) =>
        {
            await Complete(new ResolveCatletParentCommandResponse()
            {
                ParentId = Data.ParentId,
                Config = response.Config,
            });
        });
    }
}
