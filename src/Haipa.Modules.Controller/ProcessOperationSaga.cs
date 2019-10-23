using System;
using System.Reflection;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands;
using Haipa.Messages.Operations;
using Haipa.Rebus;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    internal class ProcessOperationSaga : Saga<OperationSagaData>,
        IAmInitiatedBy<CreateOperationCommand>,
        IHandleMessages<CreateNewOperationTaskCommand>,
        IHandleMessages<OperationTaskAcceptedEvent>,
        IHandleMessages<OperationTaskStatusEvent>,
        IHandleMessages<OperationTimeoutEvent>
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
            config.Correlate<CreateOperationCommand>(m => m.TaskMessage.OperationId, d => d.OperationId);
            config.Correlate<CreateNewOperationTaskCommand>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTimeoutEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskAcceptedEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent>(m => m.OperationId, d => d.OperationId);

        }

        public Task Handle(CreateOperationCommand message)
        {
            Data.PrimaryTaskId = message.TaskMessage.TaskId;
            return Handle(message.TaskMessage);
        }

        public async Task Handle(CreateNewOperationTaskCommand message)
        {
            var command = JsonConvert.DeserializeObject(message.CommandData, Type.GetType(message.CommandType));

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
                    Operation = op,
                };

                _dbContext.Add(task);
            }

            task.Name = command.GetType().Name;
            Data.Tasks.Add(message.TaskId, command.GetType().AssemblyQualifiedName);

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);

            var sendCommandAttribute = command.GetType().GetCustomAttribute<SendMessageToAttribute>();

            switch (sendCommandAttribute.Recipient)
            {
                case MessageRecipient.VMAgent:
                {
                    if (command is IMachineCommand machineCommand)
                    {

                        var machine = await _dbContext.Machines.FindAsync(machineCommand.MachineId).ConfigureAwait(false);

                        if (machine == null)
                        {

                            if (command.GetType().GetCustomAttribute(typeof(MachineMayNotExistsAttribute)) != null)
                            {
                                await Handle(OperationTaskStatusEvent.Completed(message.OperationId, message.TaskId));
                            }
                            else
                            {
                                await Handle(OperationTaskStatusEvent.Failed(message.OperationId, message.TaskId,
                                    new ErrorData {ErrorMessage = "Machine not found"}));

                            }

                            return;
                        }

                        await _bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{machine.AgentName}", command)
                            .ConfigureAwait(false);

                        return;


                    }

                    await _bus.Advanced.Topics.Publish($"broadcast_{QueueNames.VMHostAgent}", command).ConfigureAwait(false);
                    return;
                }

                case MessageRecipient.Controllers :
                    await _bus.Send(command);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Task Handle(OperationTimeoutEvent message)
        {
            return Task.CompletedTask;
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

            await _dbContext.SaveChangesAsync();

        }

        public async Task Handle(OperationTaskStatusEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);
            var task = await _dbContext.OperationTasks.FindAsync(message.TaskId).ConfigureAwait(false);

            if (op == null || task == null)
                return;

            if (task.Status == OperationTaskStatus.Queued || task.Status == OperationTaskStatus.Running)
            {
                var taskCommandTypeName = Data.Tasks[message.TaskId];

                var genericType = typeof(OperationTaskStatusEvent<>);
                var wrappedCommandType = genericType.MakeGenericType(Type.GetType(taskCommandTypeName));
                var commandInstance = Activator.CreateInstance(wrappedCommandType, message);
                await _bus.SendLocal(commandInstance);

            }

            task.Status = message.OperationFailed ? OperationTaskStatus.Failed : OperationTaskStatus.Completed;

            if (message.TaskId == Data.PrimaryTaskId)
            {
                
                op.Status = message.OperationFailed ? OperationStatus.Failed : OperationStatus.Completed;
                string errorMessage = null;
                if (message.GetMessage() is ErrorData errorData)
                    errorMessage = errorData.ErrorMessage;

                op.StatusMessage = string.IsNullOrWhiteSpace(errorMessage) ? op.Status.ToString() : errorMessage;
                MarkAsComplete();
            }


            await _dbContext.SaveChangesAsync();

        }

        //public async Task Handle(OperationCompletedEvent message)
        //{
        //    var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

        //    if (op == null)
        //        return;

        //    op.Status = message.Failed ? OperationStatus.Failed : OperationStatus.Completed;
        //    op.StatusMessage = !string.IsNullOrWhiteSpace(message.Message) ? message.Message : op.Status.ToString();

        //    await _dbContext.SaveChangesAsync();

        //    MarkAsComplete();
        //}



    }

    //[UsedImplicitly]
    //public class DefaultOperationTaskStatusEventHandler<T> : IHandleMessages<OperationTaskStatusEvent<T>> where T: OperationTaskMessage
    //{
    //    private readonly IBus _bus;

    //    public DefaultOperationTaskStatusEventHandler(IBus bus)
    //    {
    //        _bus = bus;
    //    }


    //    public async Task Handle(OperationTaskStatusEvent<T> message)
    //    {

    //        string statusMessage = null;

    //        if (message.GetMessage() is ErrorData errorData)
    //            statusMessage = errorData.ErrorMessage;

    //        await _bus.SendLocal(new OperationCompletedEvent
    //        {
    //            Failed = message.OperationFailed,
    //            Message = statusMessage,
    //            OperationId = message.OperationId
    //        }).ConfigureAwait(false);
    //    }
    //}
}