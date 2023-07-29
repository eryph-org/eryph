using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class DestroyVirtualDiskSaga : OperationTaskWorkflowSaga<DestroyVirtualDiskCommand, DestroyVirtualDiskSagaData>,
        IHandleMessages<OperationTaskStatusEvent<RemoveVirtualDiskCommand>>
    {
        private readonly IVirtualDiskDataService _virtualDiskDataService;
        private readonly IStorageManagementAgentLocator _agentLocator;

        public DestroyVirtualDiskSaga(IWorkflow workflow,
            IVirtualDiskDataService virtualDiskDataService, IStorageManagementAgentLocator agentLocator) : base(workflow)
        {
            _virtualDiskDataService = virtualDiskDataService;
            _agentLocator = agentLocator;
        }

        public Task Handle(OperationTaskStatusEvent<RemoveVirtualDiskCommand> message)
        {
            return FailOrRun(message, async () =>
            {
                await _virtualDiskDataService.DeleteVHD(Data.DiskId);

                await Complete(new DestroyResourcesResponse
                {
                    DestroyedResources = new[] { new Resource(ResourceType.VirtualDisk, Data.DiskId) },
                });
            });
        }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyVirtualDiskSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<RemoveVirtualDiskCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);

        }


        protected override async Task Initiated(DestroyVirtualDiskCommand message)
        {
            Data.DiskId = message.Resource.Id;
            var res = await _virtualDiskDataService.GetVHD(Data.DiskId);

            await res.Match(
                None: () => Complete(new DestroyResourcesResponse
                {
                    DestroyedResources = new[] { new Resource(ResourceType.VirtualDisk, Data.DiskId) },
                }),
                Some: vhd =>
                {
                    //if (vhd.AttachedDrives.Any())
                    //{
                    //    Complete(new DestroyResourcesResponse
                    //    {
                    //        DetachedResources = new[] { new Resource(ResourceType.VirtualDisk, Data.DiskId) },
                    //    });
                    //}

                    var agentName = _agentLocator.FindAgentForDataStore(vhd.DataStore, vhd.Environment);

                    return StartNewTask(new RemoveVirtualDiskCommand
                    {
                        DiskId = Data.DiskId,
                        Path = vhd.Path,
                        FileName = vhd.FileName,
                        AgentName = agentName
                    }).AsTask();
                });


        }


    }
}