using System;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.StateDb;
using HyperVPlus.StateDb.Model;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    internal class ProcessOperationSaga : Saga<OperationSagaData>,
        IAmInitiatedBy<StartOperation>,
        IHandleMessages<OperationAcceptedEvent>,
        IHandleMessages<OperationCompletedEvent>,
        IHandleMessages<OperationFailedEvent>,
        IHandleMessages<OperationTimeoutMessage>
    {
        private readonly StateStoreContext _dbContext;
        private readonly IBus _bus;

        public ProcessOperationSaga(StateStoreContext dbContext, IBus bus)
        {
            _dbContext = dbContext;
            _bus = bus;
        }


        protected override void CorrelateMessages(ICorrelationConfig<OperationSagaData> config)
        {
            config.Correlate<StartOperation>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTimeoutMessage>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationAcceptedEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationCompletedEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationFailedEvent>(m => m.OperationId, d => d.OperationId);
        }

        public async Task Handle(StartOperation message)
        {
            var command = JsonConvert.DeserializeObject(message.CommandData, Type.GetType(message.CommandType));

            if (command is IMachineCommand machineCommand)
            {
                var machine = await _dbContext.Machines.FindAsync(machineCommand.MachineId).ConfigureAwait(false);
                await _bus.Advanced.Routing.Send($"haipa.agent.{machine.AgentName}", command).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync().ConfigureAwait(false);

                return;
            }

            await _bus.Advanced.Topics.Publish("haipa.agent.all", command) .ConfigureAwait(false);

        }


        public async Task Handle(OperationTimeoutMessage message)
        {
        }

        public async Task Handle(OperationAcceptedEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

            if (op == null)
                return;

            op.Status = OperationStatus.Running;
            op.AgentName = message.AgentName;

            await _dbContext.SaveChangesAsync();

        }

        public async Task Handle(OperationCompletedEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

            if (op == null)
                return;

            op.Status = OperationStatus.Completed;

            await _dbContext.SaveChangesAsync();

            MarkAsComplete();
        }

        public async Task Handle(OperationFailedEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

            if (op == null)
                return;

            op.Status = OperationStatus.Failed;

            await _dbContext.SaveChangesAsync();

            MarkAsComplete();
        }
    }
}