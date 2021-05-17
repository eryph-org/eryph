using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations.Commands;
using Haipa.Messages.Operations.Events;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    internal class IncomingOperationHandler<T> : IHandleMessages<T> where T : IOperationTaskCommand
    {
        private readonly IBus _bus;

        public IncomingOperationHandler(IBus bus)
        {
            _bus = bus;
        }

        public async Task Handle(T message)
        {
            await _bus.SendLocal(new AcceptedOperationTaskEvent<T>(message)).ConfigureAwait(false);

            await _bus.Publish(new OperationTaskAcceptedEvent
            {
                AgentName = Environment.MachineName,
                OperationId = message.OperationId
            }).ConfigureAwait(false);
        }
    }
}