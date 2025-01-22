using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.Modules.Controller.Inventory;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DestroyVirtualDiskSaga(
    IWorkflow workflow,
    IStateStore stateStore,
    IStorageManagementAgentLocator agentLocator,
    IInventoryLockManager lockManager)
    : OperationTaskWorkflowSaga<DestroyVirtualDiskCommand, DestroyVirtualDiskSagaData>(workflow),
        IHandleMessages<OperationTaskStatusEvent<RemoveVirtualDiskCommand>>
{
    public Task Handle(OperationTaskStatusEvent<RemoveVirtualDiskCommand> message)
    {
        return FailOrRun(message, async (RemoveVirtualDiskCommandResponse response) =>
        {
            var virtualDisk = await stateStore.For<VirtualDisk>().GetByIdAsync(Data.DiskId);
            if (virtualDisk is not null)
            {
                await lockManager.AcquireVhdLock(virtualDisk.DiskIdentifier);
                virtualDisk.Deleted = true;
                virtualDisk.LastSeen = response.Timestamp;
            }

            await Complete(new DestroyResourcesResponse
            {
                DestroyedResources = [ new Resource(ResourceType.VirtualDisk, Data.DiskId) ],
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
        var virtualDisk = await stateStore.For<VirtualDisk>().GetByIdAsync(Data.DiskId);

        if (virtualDisk is null)
        {
            await Complete(new DestroyResourcesResponse
            {
                DestroyedResources = [ new Resource(ResourceType.VirtualDisk, Data.DiskId) ],
            });
            return;
        }

        await stateStore.LoadCollectionAsync(virtualDisk, d => d.AttachedDrives);
        await stateStore.LoadCollectionAsync(virtualDisk, d => d.Children);

        if (virtualDisk.GeneSet is not null
            || virtualDisk.Children.Count > 0
            || virtualDisk.AttachedDrives.Count > 0
            || virtualDisk.Frozen)
        {
            await Complete(new DestroyResourcesResponse
            {
                DetachedResources = [new Resource(ResourceType.VirtualDisk, Data.DiskId)],
            });
            return;
        }
        
        var agentName = agentLocator.FindAgentForDataStore(virtualDisk.DataStore, virtualDisk.Environment);

        await StartNewTask(new RemoveVirtualDiskCommand
        {
            DiskId = Data.DiskId,
            Path = virtualDisk.Path,
            FileName = virtualDisk.FileName,
            AgentName = agentName
        });
    }
}
