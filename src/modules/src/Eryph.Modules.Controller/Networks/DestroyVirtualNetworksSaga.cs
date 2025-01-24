using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Networks
{
    [UsedImplicitly]
    internal class DestroyVirtualNetworksSaga : OperationTaskWorkflowSaga<DestroyVirtualNetworksCommand, DestroyVirtualNetworksSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>

    {
        private readonly IStateStoreRepository<VirtualNetwork> _networkRepository;


        public DestroyVirtualNetworksSaga(IWorkflow workflow, IStateStoreRepository<VirtualNetwork> networkRepository) : base(workflow)
        {
            _networkRepository = networkRepository;
        }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyVirtualNetworksSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);

        }


        protected override async Task Initiated(DestroyVirtualNetworksCommand message)
        {
            
            var destroyedNetworks = new List<Guid>();
            var toDestroy = new List<VirtualNetwork>();
            foreach (var id in message.NetworkIds)
            {
                var networkEntity = await _networkRepository.GetByIdAsync(id);
                if (networkEntity == null)
                {
                    destroyedNetworks.Add(id);
                    continue;
                }
                toDestroy.Add(networkEntity);

            }

            if (toDestroy.Count == 0)
            {
                await Complete(new DestroyResourcesResponse
                {
                    DetachedResources = [],
                    DestroyedResources = destroyedNetworks
                        .Select(x => new Resource(ResourceType.VirtualNetwork, x)).ToArray()
                });
                return;
            }

            await _networkRepository.DeleteRangeAsync(toDestroy);
            destroyedNetworks.AddRange(toDestroy.Select(x => x.Id));
            Data.DestroyedNetworks = destroyedNetworks.ToArray();

            await StartNewTask(new UpdateNetworksCommand
            {
                Projects = toDestroy.Select(x => x.ProjectId).Distinct().ToArray()
            });

        }


        public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
        {
            return FailOrRun(message, () =>
            {
                return Complete(new DestroyResourcesResponse
                {
                    DetachedResources = [],
                    DestroyedResources = (Data.DestroyedNetworks ?? Array.Empty<Guid>())
                        .Select(x => new Resource(ResourceType.VirtualNetwork, x)).ToArray()
                });
            });

        }
    }
}