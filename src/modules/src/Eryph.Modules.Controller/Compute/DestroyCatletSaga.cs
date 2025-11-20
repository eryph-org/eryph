using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Inventory;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DestroyCatletSaga(
    IWorkflow workflow,
    IStateStoreRepository<Catlet> catletRepository,
    IInventoryLockManager lockManager,
    ICatletDataService vmDataService) :
    OperationTaskWorkflowSaga<DestroyCatletCommand, EryphSagaData<DestroyCatletSagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<RemoveCatletVMCommand>>,
    IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
{
    protected override async Task Initiated(DestroyCatletCommand message)
    {
        Data.Data.MachineId = message.Resource.Id;
        Data.Data.DestroyedResources = [new Resource(ResourceType.Catlet, message.Resource.Id)];
        var catlet = await vmDataService.Get(Data.Data.MachineId);
        if (catlet is null)
        {
            await Complete();
            return;
        }

        Data.Data.VmId = catlet.VmId;

        await StartNewTask(new RemoveCatletVMCommand
        {
            CatletId = Data.Data.MachineId,
            VmId = catlet.VmId
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveCatletVMCommand> message) =>
        FailOrRun(message, async () =>
        {
            await lockManager.AcquireVmLock(Data.Data.VmId);

            var catlet = await catletRepository.GetBySpecAsync(
                new CatletSpecs.GetForDelete(Data.Data.MachineId));
            if (catlet is null)
            {
                await CompleteWithResponse();
                return;
            }

            var attachedDisks = catlet.Drives
                .Map(d => Optional(d.AttachedDisk))
                .Somes()
                .ToSeq();
            var disksToDelete = attachedDisks
                .Filter(d => d.StorageIdentifier == catlet.StorageIdentifier)
                .Map(d => new Resource(ResourceType.VirtualDisk, d.Id));
            var disksToDetach = attachedDisks
                .Filter(d => d.StorageIdentifier != catlet.StorageIdentifier)
                .Map(d => new Resource(ResourceType.VirtualDisk, d.Id));

            Data.Data.DetachedResources = Data.Data.DetachedResources.ToSeq()
                .Append(disksToDetach)
                .ToList();

            await vmDataService.Remove(Data.Data.MachineId);

            await StartNewTask(new DestroyResourcesCommand
            {
                Resources = disksToDelete.ToArray(),
            });
        });

    public Task Handle(OperationTaskStatusEvent<DestroyResourcesCommand> message) =>
        FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            Data.Data.DestroyedResources = Data.Data.DestroyedResources
                .Append(response.DestroyedResources)
                .ToList();
            Data.Data.DetachedResources = Data.Data.DetachedResources
                .Append(response.DetachedResources)
                .ToList();

            await CompleteWithResponse();
        });

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<DestroyCatletSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<RemoveCatletVMCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DestroyResourcesCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private async Task CompleteWithResponse()
    {
        await Complete(new DestroyResourcesResponse()
        {
            DestroyedResources = Data.Data.DestroyedResources.ToArray(),
            DetachedResources = Data.Data.DetachedResources.ToArray(),
        });
    }
}
