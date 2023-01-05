using System;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Commands;
using Eryph.Messages.Operations.Events;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.ModuleCore
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
            await _bus.SendLocal(new OperationTask<T>(taskMessage.Message,  taskMessage.OperationId, taskMessage.InitiatingTaskId, taskMessage.TaskId)).ConfigureAwait(false);

            _logger.LogTrace($"Accepted incoming operation message. Operation id: '{taskMessage.OperationId}'");

            await _bus.Reply(new OperationTaskAcceptedEvent
            {
                AgentName = Environment.MachineName,
                OperationId = taskMessage.OperationId,
                InitiatingTaskId = taskMessage.InitiatingTaskId,
                TaskId = taskMessage.TaskId
            }).ConfigureAwait(false);
        }
    }
}