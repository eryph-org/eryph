using System;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations
{
    public abstract class OperationTaskWorkflowSaga<TMessage, TSagaData> : Saga<TSagaData>,
        IAmInitiatedBy<OperationTask<TMessage>>,
        IHandleMessages<OperationTaskStatusEvent<TMessage>>
        where TSagaData : TaskWorkflowSagaData, new()
        where TMessage : class, new()
    {
        protected readonly IBus Bus;

        protected OperationTaskWorkflowSaga(IBus bus)
        {
            Bus = bus;
        }


        public Task Handle(OperationTask<TMessage> message)
        {
            Data.OperationId = message.OperationId;
            Data.InitiatingTaskId = message.TaskId;
            return Initiated(message.Command);
        }

        public Task Handle(OperationTaskStatusEvent<TMessage> message)
        {
            return message.OperationFailed ? InitiatingTaskFailed() : InitiatingTaskCompleted();
        }

        protected override void CorrelateMessages(ICorrelationConfig<TSagaData> config)
        {
            config.Correlate<OperationTask<TMessage>>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<TMessage>>(m => m.OperationId, d => d.OperationId);
        }

        public abstract Task Initiated(TMessage message);

        public virtual Task InitiatingTaskCompleted()
        {
            MarkAsComplete();
            return Task.CompletedTask;
        }

        public virtual Task InitiatingTaskFailed()
        {
            MarkAsComplete();
            return Task.CompletedTask;
        }

        public Task Fail(object message = null)
        {
            return Bus.SendLocal(OperationTaskStatusEvent.Failed(Data.OperationId, Data.InitiatingTaskId, message));
        }


        public Task Complete(object message = null)
        {
            return Bus.SendLocal(OperationTaskStatusEvent.Completed(Data.OperationId, Data.InitiatingTaskId, message));
        }

        public Task FailOrRun<T>(OperationTaskStatusEvent<T> message, Func<Task> completedFunc)
            where T : class, new()
        {
            if (message.OperationFailed)
                return Fail(message.GetMessage());

            return completedFunc();
        }

        public Task FailOrRun<T, TOpMessage>(OperationTaskStatusEvent<T> message, Func<TOpMessage, Task> completedFunc)
            where T : class, new()
            where TOpMessage : class
        {
            return message.OperationFailed 
                ? Fail(message.GetMessage()) 
                : completedFunc(message.GetMessage() as TOpMessage 
                                ?? throw new InvalidOperationException(
                                    $"Message {typeof(T)} has not returned a result of type {typeof(TOpMessage)}."));
        }
    }
}