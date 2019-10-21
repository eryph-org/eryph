using System;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Operations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.Controller
{
    internal class IncomingOperationTaskHandler<T> : IHandleMessages<T> where T : IOperationTaskCommand
    {
        private readonly IBus _bus;

        public IncomingOperationTaskHandler(IBus bus)
        {
            _bus = bus;
        }

        public async Task Handle(T message)
        {
            await _bus.SendLocal(new AcceptedOperationTask<T>(message)).ConfigureAwait(false);

            await _bus.Reply(new OperationTaskAcceptedEvent
            {
                AgentName = Environment.MachineName,
                OperationId = message.OperationId,
                TaskId =  message.TaskId,
            }).ConfigureAwait(false);
        }
    }
}