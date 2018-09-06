using System;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Haipa.Modules.VmHostAgent
{
    internal class IncomingOperationHandler<T> : IHandleMessages<T> where T : OperationCommand
    {
        private readonly IBus _bus;

        public IncomingOperationHandler(IBus bus)
        {
            _bus = bus;
        }

        public async Task Handle(T message)
        {
            await _bus.SendLocal(new AcceptedOperation<T>(message)).ConfigureAwait(false);

            await _bus.Reply(new OperationAcceptedEvent
            {
                AgentName = Environment.MachineName,
                OperationId = message.OperationId
            }).ConfigureAwait(false);
        }
    }
}