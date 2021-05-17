using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Events;
using Haipa.Messages.Resources.Commands;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Primitives;
using Haipa.Primitives.Resources;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class DestroyResourcesSaga : OperationTaskWorkflowSaga<DestroyResourcesCommand, DestroyResourcesSagaData>,
        IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public DestroyResourcesSaga(IBus bus, IOperationTaskDispatcher taskDispatcher) : base(bus)
      {
          _taskDispatcher = taskDispatcher;
      }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyResourcesSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<DestroyResourcesCommand>(m => m.OperationId, m => m.OperationId);

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
                        new DestroyMachineCommand {OperationId = Data.OperationId, 
                            TaskId = Guid.NewGuid(), Resource = resource}),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }


            return Task.CompletedTask;
        }

    }
}