using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DestroyCatletSpecificationSaga(
    IStateStoreRepository<Catlet> catletRepository,
    IStateStoreRepository<CatletSpecification> specificationRepository,
    IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<DestroyCatletSpecificationCommand, EryphSagaData<DestroyCatletSpecificationSagaData>>(
            workflow),
        IHandleMessages<OperationTaskStatusEvent<DestroyCatletCommand>>
{
    public Task Handle(OperationTaskStatusEvent<DestroyCatletCommand> message)
    {
        if (Data.Data.State >= DestroyCatletSpecificationSagaState.CatletDestroyed)
            return Task.CompletedTask;

        return FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            // One event per deployment. Collect the results and only delete the specification once
            // every deployment is gone: it must not disappear while a catlet still references it.
            var destroyedCatlet = response.DestroyedResources
                .Where(r => r.Type == ResourceType.Catlet)
                .Select(r => r.Id)
                .FirstOrDefault();

            if (destroyedCatlet != Guid.Empty && !Data.Data.CatletsDestroyed.Contains(destroyedCatlet))
                Data.Data.CatletsDestroyed.Add(destroyedCatlet);

            Data.Data.DestroyedResources.AddRange(response.DestroyedResources);
            Data.Data.DetachedResources.AddRange(response.DetachedResources ?? []);

            if (Data.Data.CatletsDestroyed.Count < Data.Data.CatletIds.Length)
                return;

            Data.Data.State = DestroyCatletSpecificationSagaState.CatletDestroyed;

            await DeleteSpecificationAsync(Data.Data.SpecificationId);

            await Complete(new DestroyResourcesResponse
            {
                DestroyedResources =
                [
                    ..Data.Data.DestroyedResources,
                    new Resource(ResourceType.CatletSpecification, Data.Data.SpecificationId),
                ],
                DetachedResources = [..Data.Data.DetachedResources],
            });
        });
    }

    protected override async Task Initiated(DestroyCatletSpecificationCommand message)
    {
        Data.Data.State = DestroyCatletSpecificationSagaState.Initiated;
        Data.Data.SpecificationId = message.SpecificationId;

        // Every deployment, not just one: the specification may be deployed in several environments.
        var catlets = await catletRepository.ListAsync(
            new CatletSpecs.ListBySpecificationId(Data.Data.SpecificationId));
        if (catlets.Count == 0)
        {
            await DeleteSpecificationAsync(Data.Data.SpecificationId);
            await Complete(new DestroyResourcesResponse
            {
                DestroyedResources =
                [
                    new Resource(ResourceType.CatletSpecification, Data.Data.SpecificationId),
                ],
                DetachedResources = [],
            });
            return;
        }

        if (!message.DestroyCatlet)
        {
            await Fail(
                $"The catlet specification {Data.Data.SpecificationId} is deployed as "
                + $"{(catlets.Count == 1 ? $"catlet {catlets[0].Id}" : $"{catlets.Count} catlets")} "
                + "and cannot be deleted.");
            return;
        }

        Data.Data.CatletIds = catlets.Select(c => c.Id).ToArray();

        foreach (var catletId in Data.Data.CatletIds)
        {
            await StartNewTask(new DestroyCatletCommand
            {
                CatletId = catletId,
            });
        }
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
