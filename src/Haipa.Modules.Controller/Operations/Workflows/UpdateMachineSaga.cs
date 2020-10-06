using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class UpdateMachineSaga : OperationTaskWorkflowSaga<UpdateMachineCommand, UpdateMachineSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public UpdateMachineSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, StateStoreContext dbContext) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateMachineSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>(m => m.OperationId, d => d.OperationId);

        }

        public override Task Initiated(UpdateMachineCommand message)
        {
            Data.Config = message.Config;
            Data.MachineId = message.MachineId;

            var convergeMessage = new UpdateVirtualMachineCommand
            {
                MachineId = Data.MachineId,
                Config = Data.Config, 
                AgentName = message.AgentName, 
                OperationId = message.OperationId, 
                TaskId = Guid.NewGuid()
            };

            return _taskDispatcher.Send(convergeMessage);
        }

        public Task Handle(OperationTaskStatusEvent<UpdateVirtualMachineCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }

    }
}