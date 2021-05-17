using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations
{
    public abstract class OperationTaskWorkflowSaga<TMessage,TSagaData> : Saga<TSagaData>,
        IAmInitiatedBy<AcceptedOperationTask<TMessage>>,
        IHandleMessages<OperationTaskStatusEvent<TMessage>>

        where TSagaData : TaskWorkflowSagaData, new()
        where TMessage : OperationTaskCommand
    {
        protected readonly IBus Bus;

        protected OperationTaskWorkflowSaga(IBus bus)
        {
            Bus = bus;
        }

        protected override void CorrelateMessages(ICorrelationConfig<TSagaData> config)
        {
            config.Correlate<AcceptedOperationTask<TMessage>>(m => m.Command.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<TMessage>>(m => m.OperationId, d => d.OperationId);

        }


        public Task Handle(AcceptedOperationTask<TMessage> message)
        {
            Data.OperationId = message.Command.OperationId;
            Data.InitiatingTaskId = message.Command.TaskId;
            return Initiated(message.Command);
        }

        public Task Handle(OperationTaskStatusEvent<TMessage> message)
        {
            return message.OperationFailed ? InitiatingTaskFailed() : InitiatingTaskCompleted();
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

        public Task FailOrRun<T>(OperationTaskStatusEvent<T> message, Func<Task> completedFunc) where T: OperationTaskCommand
        {
            if (message.OperationFailed)
                return Fail(message.GetMessage());

            return completedFunc();
        }

        public Task FailOrRun<T, TOpMessage>(OperationTaskStatusEvent<T> message, Func<TOpMessage, Task> completedFunc) 
            where T : OperationTaskCommand
            where TOpMessage : class
        {
            if (message.OperationFailed)
                return Fail(message.GetMessage());

            return completedFunc(message.GetMessage() as TOpMessage);
        }


    }
}