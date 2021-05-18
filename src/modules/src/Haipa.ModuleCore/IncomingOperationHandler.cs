using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Commands;
using Haipa.Messages.Operations.Events;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.ModuleCore
{
    public class IncomingTaskMessageHandler<T> : IHandleMessages<OperationTaskSystemMessage<T>> where T: class, new()
    {
        private readonly IBus _bus;
        private readonly ILogger<IncomingTaskMessageHandler<T>> _logger;

        public IncomingTaskMessageHandler(IBus bus, ILogger<IncomingTaskMessageHandler<T>> logger)
        {
            _bus = bus;
            _logger = logger;
        }

        public async Task Handle(OperationTaskSystemMessage<T> taskMessage)
        {
            await _bus.SendLocal(new OperationTask<T>(taskMessage.Message, taskMessage.OperationId, taskMessage.TaskId)).ConfigureAwait(false);

            _logger.LogTrace($"Accepted incoming operation message. Operation id: '{taskMessage.OperationId}'");

            await _bus.Reply(new OperationTaskAcceptedEvent
            {
                AgentName = Environment.MachineName,
                OperationId = taskMessage.OperationId,
                TaskId = taskMessage.TaskId
            }).ConfigureAwait(false);
        }
    }
}