using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Commands;
using Eryph.Messages.Resources.Events;
using Eryph.Modules.HostAgent.Inventory;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.HostAgent;

[UsedImplicitly]
public class VerifyPlacementCalculationCommandHandler(
    IBus bus,
    IHostArchitectureProvider hostArchitectureProvider,
    WorkflowOptions workflowOptions)
    : IHandleMessages<VerifyPlacementCalculationCommand>
{
    public Task Handle(VerifyPlacementCalculationCommand message)
    {
        //this is a placeholder for a real verification that should make sure 
        //placement data used for calculation can be confirmed by agent

        return bus.SendWorkflowEvent(workflowOptions,new PlacementVerificationCompletedEvent
        {
            AgentName = Environment.MachineName,
            Architecture = hostArchitectureProvider.Architecture,
            Confirmed = true,
            CorrelationId = message.CorrelationId
        });
    }
}