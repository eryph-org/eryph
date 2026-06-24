using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Rebus;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Networks;

[UsedImplicitly]
internal class UpdateNetworksSaga(IWorkflow workflow, IBus bus)
    : OperationTaskWorkflowSaga<UpdateNetworksCommand, UpdateNetworksSagaData>(workflow),
        IHandleMessages<OperationTaskStatusEvent<UpdateProjectNetworkPlanCommand>>

{
    public Task Handle(OperationTaskStatusEvent<UpdateProjectNetworkPlanCommand> message)
    {
        return FailOrRun<UpdateProjectNetworkPlanCommand, UpdateProjectNetworkPlanResponse>(message,
            async response =>
            {
                Data.ProjectsCompleted ??= [];
                Data.UpdatedAddresses ??= [];
                // ignore if already received
                if (Data.ProjectsCompleted.Contains(response.ProjectId))
                    return;

                Data.ProjectsCompleted.Add(response.ProjectId);
                Data.UpdatedAddresses.AddRange(response.UpdatedAddresses);
                if (Data.ProjectsCompleted.Count == Data.ProjectIds?.Length)
                {
                    await bus.Advanced.Topics.Publish(
                        $"broadcast_{QueueNames.VMHostAgent}",
                        new NetworkNeighborsUpdateRequestedEvent
                        {
                            UpdatedAddresses = Data.UpdatedAddresses
                                .ToArray(),
                        });
                    await Complete();
                }
            });
    }

    protected override void CorrelateMessages(ICorrelationConfig<UpdateNetworksSagaData> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<UpdateProjectNetworkPlanCommand>>(m => m.InitiatingTaskId,
            d => d.SagaTaskId);
    }

    protected override async Task Initiated(UpdateNetworksCommand message)
    {
        Data.ProjectIds = message.Projects;

        foreach (var project in message.Projects)
            await StartNewTask(
                new UpdateProjectNetworkPlanCommand
                {
                    ProjectId = project,
                });
    }
}
