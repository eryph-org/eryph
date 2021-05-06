using System;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Events;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class DestroyResourcesSaga : OperationTaskWorkflowSaga<DestroyResourcesCommand, DestroyResourcesSagaData>,
        IHandleMessages<OperationTaskStatusEvent<DestroyMachineCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public DestroyResourcesSaga(IBus bus, IOperationTaskDispatcher taskDispatcher) : base(bus)
      {
          _taskDispatcher = taskDispatcher;
      }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyResourcesSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<DestroyMachineCommand>(m => m.OperationId, m => m.OperationId);

        }


        public override Task Initiated(DestroyResourcesCommand message)
        {
            Data.State = DestroyResourceState.Initiated;
            Data.Resources = message.Resources;

            
            foreach (var resource in Data.Resources)
            {
                return resource.Type switch
                {
                    ResourceType.Machine => _taskDispatcher.Send(
                        new DestroyMachineCommand {OperationId = Data.OperationId, TaskId = Guid.NewGuid(),}),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }


            return Task.CompletedTask;
        }

        public Task Handle(OperationTaskStatusEvent<DestroyMachineCommand> message)
        {

        }
    }
}