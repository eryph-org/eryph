using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Commands;
using Eryph.Messages.Resources.Events;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class VerifyPlacementCalculationCommandHandler : IHandleMessages<VerifyPlacementCalculationCommand>
    {
        private readonly IBus _bus;
        private readonly WorkflowOptions _workflowOptions;

        public VerifyPlacementCalculationCommandHandler(IBus bus, WorkflowOptions workflowOptions)
        {
            _bus = bus;
            _workflowOptions = workflowOptions;
        }

        public Task Handle(VerifyPlacementCalculationCommand message)
        {
            //this is a placeholder for a real verification that should make sure 
            //placement data used for calculation can be confirmed by agent

            return _bus.SendWorkflowEvent(_workflowOptions,new PlacementVerificationCompletedEvent
            {
                AgentName = Environment.MachineName,
                Confirmed = true,
                CorrelationId = message.CorrelationId
            });
        }
    }
}