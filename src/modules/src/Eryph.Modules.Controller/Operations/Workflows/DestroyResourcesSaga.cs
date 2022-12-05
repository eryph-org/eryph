using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Resources;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class DestroyResourcesSaga : 
        OperationTaskWorkflowSaga<DestroyResourcesCommand, DestroyResourcesSagaData>,
        IHandleMessages<OperationTaskStatusEvent<DestroyCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DestroyVirtualDiskCommand>>

    {
        private readonly IOperationTaskDispatcher _taskDispatcher;

        public DestroyResourcesSaga(IBus bus, IOperationTaskDispatcher taskDispatcher) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
        }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyResourcesSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<DestroyCatletCommand>>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<DestroyVirtualDiskCommand>>(m => m.OperationId, d => d.OperationId);

        }

        protected override Task Initiated(DestroyResourcesCommand message)
        {
            Data.State = DestroyResourceState.Initiated;
            Data.Resources = message.Resources;


            foreach (var resource in Data.Resources)
                return resource.Type switch
                {
                    ResourceType.Machine => _taskDispatcher.StartNew<DestroyCatletCommand>(Data.OperationId, resource),
                    ResourceType.VirtualDisk => _taskDispatcher.StartNew<DestroyVirtualDiskCommand>(Data.OperationId, resource),
                    _ => throw new ArgumentOutOfRangeException()
                };


            return Task.CompletedTask;
        }

        public Task Handle(OperationTaskStatusEvent<DestroyVirtualDiskCommand> message)
        {
            return FailOrRun<DestroyVirtualDiskCommand, DestroyResourcesResponse>(message,
                (response) => CollectAndCheckCompleted(response.DestroyedResources, response.DetachedResources));
        }

        public Task Handle(OperationTaskStatusEvent<DestroyCatletCommand> message)
        {
            return FailOrRun<DestroyCatletCommand, DestroyResourcesResponse>(message,
                (response) => CollectAndCheckCompleted(response.DestroyedResources, response.DetachedResources));
        }

        private Task CollectAndCheckCompleted(Resource[]? destroyedResources, Resource[]? detachedResources)
        {
            if (destroyedResources != null) Data.DestroyedResources.AddRange(destroyedResources);

            if (detachedResources != null) Data.DetachedResources.AddRange(detachedResources);

            var pendingResources = (Data.Resources ?? Array.Empty<Resource>()).ToList();

            foreach (var resource in Data.DestroyedResources.Concat(Data.DetachedResources))
            {
                if (pendingResources.Contains(resource))
                    pendingResources.Remove(resource);
            }

            if (pendingResources.Count == 0)
            {
                return Complete(new DestroyResourcesResponse
                {
                    DestroyedResources = Data.DestroyedResources.ToArray(),
                    DetachedResources = Data.DetachedResources.ToArray()
                });
            }

            return Task.CompletedTask;
        }
    }
}