using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.Operations;
using Eryph.Resources;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class DestroyResourcesSaga :
        OperationTaskWorkflowSaga<DestroyResourcesCommand, DestroyResourcesSagaData>,
        IHandleMessages<OperationTaskStatusEvent<DestroyCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DestroyVirtualDiskCommand>>
    {

        public DestroyResourcesSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IMessageContext messageContext ) : base(bus, taskDispatcher, messageContext)
        {
        }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyResourcesSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<DestroyCatletCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<DestroyVirtualDiskCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);

        }

        protected override Task Initiated(DestroyResourcesCommand message)
        {
            Data.State = DestroyResourceState.Initiated;
            Data.Resources = message.Resources;

            var firstGroup = new List<Resource>();
            var secondGroup = new List<Resource>();


            foreach (var resource in Data.Resources)
                switch(resource.Type)
                {
                    case ResourceType.Catlet:
                        firstGroup.Add(resource);
                        break;

                    case ResourceType.VirtualDisk:
                        secondGroup.Add(resource);
                        break;
                    case ResourceType.VirtualNetwork:
                        //secondGroup.Add(resource);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                };

            Data.DestroyGroups = new List<List<Resource>>();
            
            if(firstGroup.Count>0)
                Data.DestroyGroups.Add(firstGroup);

            if (secondGroup.Count > 0)
                Data.DestroyGroups.Add(secondGroup);

            return DestroyNextGroup();



        }

        private async Task DestroyNextGroup()
        {
            if (Data.DestroyGroups.Count == 0)
            {
                await Complete(new DestroyResourcesResponse
                {
                    DestroyedResources = Data.DestroyedResources.ToArray(),
                    DetachedResources = Data.DetachedResources.ToArray()
                });
                return;
            }

            Data.Resources = Data.DestroyGroups[0].ToArray();
            Data.DestroyGroups.RemoveAt(0);

            foreach (var resource in Data.Resources)
                switch(resource.Type)
                {

                    case ResourceType.Catlet:
                        await StartNewTask<DestroyCatletCommand>(resource);
                        break;
                    case ResourceType.VirtualDisk:
                        await StartNewTask<DestroyVirtualDiskCommand>(resource);
                        break;
                    case ResourceType.VirtualNetwork:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                };


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
                return DestroyNextGroup();
            }

            return Task.CompletedTask;
        }
    }
}