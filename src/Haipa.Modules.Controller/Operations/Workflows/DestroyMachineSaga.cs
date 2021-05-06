using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class DestroyMachineSaga : OperationTaskWorkflowSaga<DestroyMachineCommand, DestroyMachineSagaData>,
        IHandleMessages<OperationTaskStatusEvent<RemoveVMCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public DestroyMachineSaga(IBus bus, IOperationTaskDispatcher taskDispatcher) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
        }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyMachineSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<RemoveVMCommand>>(m => m.OperationId, d => d.OperationId);

        }


        public override Task Initiated(DestroyMachineCommand message)
        {
            Data.MachineId = message.Resource.Id;


        }

        public Task Handle(OperationTaskStatusEvent<RemoveVMCommand> message)
        {
            return FailOrRun<RemoveVMCommand>(message, () =>
            {
                return Complete(new DestroyResourceResponse
                {
                    DestroyedResources = new[] {new Resource(ResourceType.Machine, Data.MachineId) },
                    DetachedResources = 
                });
            });
        }
    }
}