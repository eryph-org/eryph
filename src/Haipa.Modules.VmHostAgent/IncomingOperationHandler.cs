using System;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Operations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;

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
            await _bus.SendLocal(new AcceptedOperationTask<T>(message)).ConfigureAwait(false);

            await _bus.Publish(new OperationTaskAcceptedEvent
            {
                AgentName = Environment.MachineName,
                OperationId = message.OperationId
            }).ConfigureAwait(false);
        }
    }
}