using System;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Commands;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.ModuleCore;
using Eryph.Resources;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class DestroyResourcesSaga : OperationTaskWorkflowSaga<DestroyResourcesCommand, DestroyResourcesSagaData>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public DestroyResourcesSaga(IBus bus, IOperationTaskDispatcher taskDispatcher) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
        }

        protected override Task Initiated(DestroyResourcesCommand message)
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