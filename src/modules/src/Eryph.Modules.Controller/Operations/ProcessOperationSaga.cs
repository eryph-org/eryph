using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations.Commands;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources;
using Eryph.Messages.Resources.Catlets;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations
{
    [UsedImplicitly]
    internal class ProcessOperationSaga : Saga<OperationSagaData>,
        IAmInitiatedBy<CreateOperationCommand>,
        IHandleMessages<CreateNewOperationTaskCommand>,
        IHandleMessages<OperationTaskAcceptedEvent>,
        IHandleMessages<OperationTaskStatusEvent>,
        IHandleMessages<OperationTimeoutEvent>
    {
        private readonly IBus _bus;
        private readonly StateStoreContext _dbContext;

        public ProcessOperationSaga(StateStoreContext dbContext, IBus bus)
        {
            _dbContext = dbContext;
            _bus = bus;
        }

        public Task Handle(CreateOperationCommand message)
        {
            Data.PrimaryTaskId = message.TaskMessage.TaskId;
            return Handle(message.TaskMessage);
        }

        public async Task Handle(CreateNewOperationTaskCommand message)
        {
            var command = JsonSerializer.Deserialize(message.CommandData,
                Type.GetType(message.CommandType) ??
                throw new InvalidOperationException($"unknown command type '{message.CommandType}'"));

            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);
            if (op == null)
            {
                MarkAsComplete();
                return;
            }

            var task = await _dbContext.OperationTasks.FindAsync(message.TaskId).ConfigureAwait(false);
            if (task == null)
            {
                task = new OperationTask
                {
                    Id = message.TaskId,
                    Operation = op
                };

                _dbContext.Add(task);
            }

            var messageType = Type.GetType(message.CommandType);
            if (messageType == null)
                throw new InvalidOperationException($"unknown command type '{message.CommandType}'");

            task.Name = messageType.Name;
            Data.Tasks.Add(message.TaskId, messageType.AssemblyQualifiedName!);

            var outboundMessage = Activator.CreateInstance(
                typeof(OperationTaskSystemMessage<>).MakeGenericType(messageType),
                command, message.OperationId, message.InitiatingTaskId, message.TaskId);


            var sendCommandAttribute = messageType.GetCustomAttribute<SendMessageToAttribute>();

            if (sendCommandAttribute == null)
                throw new InvalidOperationException(
                    $"Invalid command type '{messageType}'. Type has to be decorated with SendMessageTo attribute.");

            switch (sendCommandAttribute.Recipient)
            {
                case MessageRecipient.VMHostAgent:
                {
                    switch (command)
                    {
                        case IVMCommand vmCommand:
                        {
                            var machine = await _dbContext.VirtualCatlets.FindAsync(vmCommand.CatletId)
                                .ConfigureAwait(false);

                            if (machine == null)
                            {
                                await Handle(OperationTaskStatusEvent.Failed(message.OperationId,
                                    message.InitiatingTaskId, message.TaskId,
                                    new ErrorData { ErrorMessage = $"Virtual catlet {vmCommand.CatletId} not found" }));

                                return;
                            }

                            await _bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{machine.AgentName}",
                                    outboundMessage)
                                .ConfigureAwait(false);

                            return;
                        }
                        case IHostAgentCommand agentCommand:
                            await _bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{agentCommand.AgentName}",
                                    outboundMessage)
                                .ConfigureAwait(false);

                            return;
                        default:
                            throw new InvalidDataException(
                                $"Don't know how to route operation task command of type {messageType}");
                    }
                }

                case MessageRecipient.Controllers:
                    await _bus.SendLocal(outboundMessage);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        public async Task Handle(OperationTaskAcceptedEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

            var task = await _dbContext.OperationTasks.FindAsync(message.TaskId).ConfigureAwait(false);

            if (op == null || task == null)
                return;

            op.Status = OperationStatus.Running;
            op.StatusMessage = OperationStatus.Running.ToString();

            task.Status = OperationTaskStatus.Running;
            task.AgentName = message.AgentName;
        }

        public async Task Handle(OperationTaskStatusEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);
            var task = await _dbContext.OperationTasks.FindAsync(message.TaskId).ConfigureAwait(false);

            if (op == null || task == null)
                return;

            if (task.Status is OperationTaskStatus.Queued or OperationTaskStatus.Running)
            {
                var taskCommandTypeName = Data.Tasks[message.TaskId];

                var genericType = typeof(OperationTaskStatusEvent<>);
                var wrappedCommandType = genericType.MakeGenericType(Type.GetType(taskCommandTypeName)
                                                                     ?? throw new InvalidOperationException(
                                                                         $"Unknown task command type '{taskCommandTypeName}'."));

                var commandInstance = Activator.CreateInstance(wrappedCommandType, message);
                await _bus.SendLocal(commandInstance);
            }

            task.Status = message.OperationFailed ? OperationTaskStatus.Failed : OperationTaskStatus.Completed;

            if (message.TaskId == Data.PrimaryTaskId)
            {
                op.Status = message.OperationFailed ? OperationStatus.Failed : OperationStatus.Completed;
                string? errorMessage = null;
                if (message.GetMessage() is ErrorData errorData)
                    errorMessage = errorData.ErrorMessage;

                op.StatusMessage = string.IsNullOrWhiteSpace(errorMessage) ? op.Status.ToString() : errorMessage;


                if (message.GetMessage() is ProjectReference projectReference)
                {
                    op.Projects ??= new List<OperationProject>();
                    op.Projects.Add(new OperationProject { Id = new Guid(), ProjectId = projectReference.ProjectId });
                }

                MarkAsComplete();
            }
        }

        public Task Handle(OperationTimeoutEvent message)
        {
            return Task.CompletedTask;
        }


        protected override void CorrelateMessages(ICorrelationConfig<OperationSagaData> config)
        {
            config.Correlate<CreateOperationCommand>(m => m.TaskMessage.OperationId, d => d.OperationId);
            config.Correlate<CreateNewOperationTaskCommand>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTimeoutEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskAcceptedEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent>(m => m.OperationId, d => d.OperationId);
        }
    }
}