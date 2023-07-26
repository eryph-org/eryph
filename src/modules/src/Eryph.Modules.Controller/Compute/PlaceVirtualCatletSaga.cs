using System;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.Messages.Resources.Events;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.Operations;
using Eryph.Rebus;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class PlaceVirtualCatletSaga :
        OperationTaskWorkflowSaga<PlaceVirtualCatletCommand, PlaceVirtualCatletSagaData>,
        IHandleMessages<PlacementVerificationCompletedEvent>
    {
        private readonly IPlacementCalculator _placementCalculator;

        public PlaceVirtualCatletSaga(IBus bus, IPlacementCalculator placementCalculator, IMessageContext messageContext,
            IOperationTaskDispatcher taskDispatcher) : base(bus, taskDispatcher, messageContext)
        {
            _placementCalculator = placementCalculator;
        }


        public Task Handle(PlacementVerificationCompletedEvent message)
        {
            // This is not implemented fully - in case not confirmed some state has to be changed too, because
            // otherwise calculation most likely will choose same agent again resulting in a loop.
            // Currently this can not happen, as placement will always be confirmed by agent.
            return message.Confirmed
                ? Complete(new PlaceVirtualCatletResult { AgentName = message.AgentName })
                : CalculatePlacementAndRequestVerification();
        }

        protected override void CorrelateMessages(ICorrelationConfig<PlaceVirtualCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<PlacementVerificationCompletedEvent>(m => m.CorrelationId, m => m.CorrelationId);
        }

        protected override Task Initiated(PlaceVirtualCatletCommand message)
        {
            Data.Config = message.Config;
            return CalculatePlacementAndRequestVerification();
        }

        private Task CalculatePlacementAndRequestVerification()
        {
            var placementCandidate = _placementCalculator.CalculateVMPlacement(Data.Config);

            if (string.IsNullOrWhiteSpace(placementCandidate))
                Fail(new ErrorData { ErrorMessage = "Failed to find any candidate for VM placement" });

            //request verification by VM Agent
            Data.CorrelationId = Guid.NewGuid();

            return Bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{placementCandidate}",
                new VerifyPlacementCalculationCommand { CorrelationId = Data.CorrelationId });
        }
    }
}