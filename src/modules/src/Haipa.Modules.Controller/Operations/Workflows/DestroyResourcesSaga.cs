using System;
using System.Threading.Tasks;
using Haipa.Messages.Resources.Commands;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.ModuleCore;
using Haipa.Resources;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class DestroyResourcesSaga : OperationTaskWorkflowSaga<DestroyResourcesCommand, DestroyResourcesSagaData>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public DestroyResourcesSaga(IBus bus, IOperationTaskDispatcher taskDispatcher) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
        }

        public override Task Initiated(DestroyResourcesCommand message)
        {
            Data.State = DestroyResourceState.Initiated;
            Data.Resources = message.Resources;


            foreach (var resource in Data.Resources)
                return resource.Type switch
                {
                    ResourceType.Machine => _taskDispatcher.StartNew<DestroyMachineCommand>(Data.OperationId, resource),
                    _ => throw new ArgumentOutOfRangeException()
                };


            return Task.CompletedTask;
        }
    }
}