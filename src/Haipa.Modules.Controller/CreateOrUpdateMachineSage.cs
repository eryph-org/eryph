using System;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    internal class CreateOrUpdateMachineSage : OperationTaskWorkflowSaga<CreateOrUpdateMachineCommand, CreateOrUpdateMachineSagaData>,        
        IHandleMessages<OperationTaskStatusEvent<ConvergeVirtualMachineCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public CreateOrUpdateMachineSage(IBus bus, IOperationTaskDispatcher taskDispatcher) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CreateOrUpdateMachineSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<ConvergeVirtualMachineCommand>>(m => m.OperationId, d => d.OperationId);

        }

        public override Task Initiated(CreateOrUpdateMachineCommand message)
        {
            var convergeMessage = new ConvergeVirtualMachineCommand
                { Config = message.Config, OperationId = message.OperationId, TaskId = Guid.NewGuid() };

            return _taskDispatcher.Send(convergeMessage);
        }

        public Task Handle(OperationTaskStatusEvent<ConvergeVirtualMachineCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }


    }

    internal interface IOperationTaskDispatcher
    {
        Task Send(OperationTaskCommand message);
    }

    class OperationTaskDispatcher : IOperationTaskDispatcher
    {
        private readonly IBus _bus;

        public OperationTaskDispatcher(IBus bus)
        {
            _bus = bus;
        }

        public Task Send(OperationTaskCommand message)
        {
            var commandJson = JsonConvert.SerializeObject(message);

            return _bus.SendLocal(
                new CreateNewOperationTaskCommand(
                    message.GetType().AssemblyQualifiedName,
                    commandJson, message.OperationId,
                    message.TaskId));
        }
    }
}