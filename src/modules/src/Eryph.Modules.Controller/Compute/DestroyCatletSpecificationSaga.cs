using Ardalis.Specification;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Sagas;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Eryph.Resources;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DestroyCatletSpecificationSaga(
    IStateStoreRepository<Catlet> catletRepository,
    IStateStoreRepository<CatletSpecification> specificationRepository,
    IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<DestroyCatletSpecificationCommand, EryphSagaData<DestroyCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<DestroyCatletCommand>>
{

    protected override async Task Initiated(DestroyCatletSpecificationCommand message)
    {
        Data.Data.State = DestroyCatletSpecificationSagaState.Initiated;
        Data.Data.SpecificationId = message.SpecificationId;

        var catlet = await catletRepository.GetBySpecAsync(
            new CatletSpecs.GetBySpecificationId(Data.Data.SpecificationId));
        if (catlet is null)
        {
            await DeleteSpecificationAsync(Data.Data.SpecificationId);
            await Complete(new DestroyResourcesResponse()
            {
                DestroyedResources =
                [
                    new Resource(ResourceType.CatletSpecification, Data.Data.SpecificationId)
                ],
                DetachedResources = [],
            });
            return;
        }

        if (!message.DestroyCatlet)
        {
            await Fail($"The catlet specification {Data.Data.SpecificationId} is deployed as catlet {catlet.Id} and cannot be deleted.");
        }

        await StartNewTask(new DestroyCatletCommand
        {
            CatletId = catlet.Id,
        });
    }

    public Task Handle(OperationTaskStatusEvent<DestroyCatletCommand> message)
    {
        if (Data.Data.State >= DestroyCatletSpecificationSagaState.CatletDestroyed)
            return Task.CompletedTask;

        return FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            Data.Data.State = DestroyCatletSpecificationSagaState.CatletDestroyed;

            await DeleteSpecificationAsync(Data.Data.SpecificationId);
            
            await Complete(new DestroyResourcesResponse
            {
                DestroyedResources =
                [
                    ..response.DestroyedResources,
                    new Resource(ResourceType.CatletSpecification, Data.Data.SpecificationId)
                ],
                DetachedResources = response.DetachedResources,
            });
        });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<DestroyCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<DestroyCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private async Task DeleteSpecificationAsync(
        Guid specificationId)
    {
        var versions = await specificationVersionRepository.ListAsync(
            new CatletSpecificationVersionSpecs.ListBySpecificationId(specificationId));
        await specificationVersionRepository.DeleteRangeAsync(versions);

        var specification = await specificationRepository.GetByIdAsync(specificationId);
        if (specification is null)
            return;

        await specificationRepository.DeleteAsync(specification);
    }
}
