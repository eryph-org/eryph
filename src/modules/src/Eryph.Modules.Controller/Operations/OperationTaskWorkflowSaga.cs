using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.ModuleCore;
using Eryph.StateDb.Model;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Operations
{
    public abstract class OperationTaskWorkflowSaga<TMessage, TSagaData> : Saga<TSagaData>,
        IAmInitiatedBy<OperationTask<TMessage>>,
        IHandleMessages<OperationTaskStatusEvent<TMessage>>
        where TSagaData : TaskWorkflowSagaData, new()
        where TMessage : class, new()
    {
        protected readonly IBus Bus;
        protected readonly IOperationTaskDispatcher TaskDispatcher;

        protected OperationTaskWorkflowSaga(IBus bus, IOperationTaskDispatcher taskDispatcher)
        {
            Bus = bus;
            TaskDispatcher = taskDispatcher;
        }


        public Task Handle(OperationTask<TMessage> message)
        {
            Data.OperationId = message.OperationId;
            Data.SagaTaskId = message.TaskId;
            Data.ParentTaskId = message.InitiatingTaskId;
            return Initiated(message.Command);
        }

        public Task Handle(OperationTaskStatusEvent<TMessage> message)
        {
            return message.OperationFailed ? InitiatingTaskFailed() : InitiatingTaskCompleted();
        }

        protected override void CorrelateMessages(ICorrelationConfig<TSagaData> config)
        {
            config.Correlate<OperationTask<TMessage>>(m => m.TaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<TMessage>>(m => m.TaskId, d => d.SagaTaskId);
        }

        protected abstract Task Initiated(TMessage message);

        private Task InitiatingTaskCompleted()
        {
            MarkAsComplete();
            return Task.CompletedTask;
        }

        private Task InitiatingTaskFailed()
        {
            MarkAsComplete();
            return Task.CompletedTask;
        }

        protected Task Fail(object? message = null)
        {
            return Bus.SendLocal(OperationTaskStatusEvent.Failed(
                Data.OperationId, Data.ParentTaskId, Data.SagaTaskId, message));
        }


        protected Task Complete(object? message = null)
        {
            return Bus.SendLocal(OperationTaskStatusEvent.Completed(
                Data.OperationId, Data.ParentTaskId, Data.SagaTaskId, message));
        }

        protected Task FailOrRun<T>(OperationTaskStatusEvent<T> message, Func<Task> completedFunc)
            where T : class, new()
        {
            if (message.OperationFailed)
                return Fail(message.GetMessage());

            return completedFunc();
        }

        protected Task FailOrRun<T, TOpMessage>(OperationTaskStatusEvent<T> message, Func<TOpMessage, Task> completedFunc)
            where T : class, new()
            where TOpMessage : class
        {
            return message.OperationFailed 
                ? Fail(message.GetMessage()) 
                : completedFunc(message.GetMessage() as TOpMessage 
                                ?? throw new InvalidOperationException(
                                    $"Message {typeof(T)} has not returned a result of type {typeof(TOpMessage)}."));
        }


        protected Task<Operation?> StartNewTask<T>(Resource resource = default) where T : class, new()
        {
            return TaskDispatcher.StartNew<T>(Data.OperationId, Data.SagaTaskId, resource);
        }

        protected Task<IEnumerable<Operation>> StartNewTask<T>(params Resource[] resources)
            where T : class, new()
        {
            return TaskDispatcher.StartNew<T>(Data.OperationId, Data.SagaTaskId, resources);

        }

        protected Task<Operation?> StartNewTask(Type operationCommandType,
            Resource resource = default)
        {
            return TaskDispatcher.StartNew(Data.OperationId, Data.SagaTaskId, operationCommandType, resource);

        }

        protected Task<IEnumerable<Operation>> StartNewTask(Type operationCommandType, params Resource[] resources)
        {
            return TaskDispatcher.StartNew(Data.OperationId, Data.SagaTaskId, operationCommandType, resources);

        }

        protected Task<Operation?> StartNewTask(object command)
        {
            return TaskDispatcher.StartNew(Data.OperationId, Data.SagaTaskId, command);

        }

        protected Task<IEnumerable<Operation>> StartNewTask(object command, params Resource[] resources)
        {
            return TaskDispatcher.StartNew(Data.OperationId, Data.SagaTaskId, command, resources);

        }


    }
}