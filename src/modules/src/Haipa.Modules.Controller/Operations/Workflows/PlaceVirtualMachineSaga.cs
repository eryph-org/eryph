using System;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Operations;
using Haipa.Messages.Resources.Commands;
using Haipa.Messages.Resources.Events;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Rebus;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class PlaceVirtualMachineSaga : OperationTaskWorkflowSaga<PlaceVirtualMachineCommand, PlaceVirtualMachineSagaData>,
        IHandleMessages<PlacementVerificationCompletedEvent>
    {

        private readonly IPlacementCalculator _placementCalculator;

        public PlaceVirtualMachineSaga(IBus bus, IPlacementCalculator placementCalculator) : base(bus)
        {
            _placementCalculator = placementCalculator;
        }

        protected override void CorrelateMessages(ICorrelationConfig<PlaceVirtualMachineSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<PlacementVerificationCompletedEvent>(m => m.CorrelationId, m=> m.CorrelationId);
        }

        public override Task Initiated(PlaceVirtualMachineCommand message)
        {
            Data.Config = message.Config;
            return CalculatePlacementAndRequestVerification();
        }

        private Task CalculatePlacementAndRequestVerification()
        {
            var placementCandidate = _placementCalculator.CalculateVMPlacement(Data.Config);

            if (string.IsNullOrWhiteSpace(placementCandidate))
                Fail(new ErrorData {ErrorMessage = "Failed to find any candidate for VM placement"});

            //request verification by VM Agent
            Data.CorrelationId = new Guid();

            return Bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{placementCandidate}",
                new VerifyPlacementCalculationCommand {CorrelationId = Data.CorrelationId});
        }


        public Task Handle(PlacementVerificationCompletedEvent message)
        {
            // This is not implemented fully - in case not confirmed some state has to be changed too, because
            // otherwise calculation most likely will choose same agent again resulting in a loop.
            // Currently this can not happen, as placement will always be confirmed by agent.
            return message.Confirmed ? 
                    Complete(new PlaceVirtualMachineResult { AgentName = message.AgentName }) : 
                    CalculatePlacementAndRequestVerification();
        }
    }
}