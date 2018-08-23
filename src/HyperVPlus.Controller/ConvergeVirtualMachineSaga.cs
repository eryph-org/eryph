using System;
using System.Linq;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.StateDb;
using HyperVPlus.StateDb.Model;
using HyperVPlus.StateDb.MySql;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace HyperVPlus.ConfigConsole
{

    internal class ConvergeVirtualMachineSaga : Saga<CollectLegalInfoSagaData>,
        IAmInitiatedBy<InitiateVirtualMachineConvergeCommand>,
        IHandleMessages<TimeoutMessage>,
        IHandleMessages<ConvergeVirtualMachineProgressEvent>

    {
        private readonly IBus _bus;
        private readonly StateStoreContext _stateStoreContext;

        public ConvergeVirtualMachineSaga(IBus bus, StateStoreContext stateStoreContext)
        {
            _bus = bus;
            _stateStoreContext = stateStoreContext;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CollectLegalInfoSagaData> config)
        {
            // ensure idempotency by setting up correlation for this one in addition to
            // allowing CustomerCreated to initiate a new saga instance
            config.Correlate<InitiateVirtualMachineConvergeCommand>(m => m.ConvergeProcessId, d => d.CorrelationId);

            // ensure proper correlation for the other messages
            config.Correlate<TimeoutMessage>(m => m.CorrelationId, d => d.CorrelationId);
            config.Correlate<ConvergeVirtualMachineProgressEvent>(m => m.CorellationId, d => d.CorrelationId);

        }

        public async Task Handle(InitiateVirtualMachineConvergeCommand message)
        {
            if (!IsNew)
                return;
            
            //await _bus.DeferLocal(new TimeSpan(0, 0, 10),
            //    new TimeoutMessage {CorrelationId = message.ConvergeProcessId});

            await _bus.Advanced.Topics.Publish("topic.agent.localhost",
                new ConvergeVirtualMachineRequestedEvent
                {
                    Config = message.Config.VirtualMachine,
                    CorellationId = message.ConvergeProcessId,
                }).ConfigureAwait(false);

        }


        public Task Handle(TimeoutMessage message)
        {
            Console.WriteLine("Failed");

            MarkAsComplete();
            return Task.CompletedTask;
        }

        public Task Handle(ConvergeVirtualMachineProgressEvent message)
        {
            var operation = _stateStoreContext.Operations.FirstOrDefault(op => op.Id == message.CorellationId);
            if (operation != null)
            {

                var opLogEntry =
                    new OperationLog {Id = Guid.NewGuid(), Message = message.Message, Operation = operation};

                _stateStoreContext.Add(opLogEntry);
                _stateStoreContext.SaveChanges();
            }

            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }
    }
}