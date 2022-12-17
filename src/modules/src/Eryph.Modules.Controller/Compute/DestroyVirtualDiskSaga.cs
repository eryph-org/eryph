using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Operations;
using Eryph.Resources;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class DestroyVirtualDiskSaga : OperationTaskWorkflowSaga<DestroyVirtualDiskCommand, DestroyVirtualDiskSagaData>,
        IHandleMessages<OperationTaskStatusEvent<RemoveVirtualDiskCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualDiskDataService _virtualDiskDataService;
        private readonly IStorageManagementAgentLocator _agentLocator;

        public DestroyVirtualDiskSaga(IBus bus, IOperationTaskDispatcher taskDispatcher,
            IVirtualDiskDataService virtualDiskDataService, IStorageManagementAgentLocator agentLocator) : base(bus, taskDispatcher)
        {
            _taskDispatcher = taskDispatcher;
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
                    });
                });


        }


    }
}