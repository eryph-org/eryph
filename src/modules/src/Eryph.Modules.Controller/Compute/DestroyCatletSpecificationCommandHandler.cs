using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Messages.Resources.Commands;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DestroyCatletSpecificationCommandHandler(
    IStateStoreRepository<Catlet> catletRepository,
    IStateStoreRepository<CatletSpecification> specificationRepository,
    IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    ITaskMessaging messaging)
    : IHandleMessages<OperationTask<DestroyCatletSpecificationCommand>>
{
    public async Task Handle(OperationTask<DestroyCatletSpecificationCommand> message)
    {
        var specification = await specificationRepository.GetByIdAsync(message.Command.SpecificationId);
        if (specification is not null)
        {
            var catlet = await catletRepository.GetBySpecAsync(
                new CatletSpecs.GetBySpecificationId(specification.Id));
            if (catlet is not null)
            {
                await messaging.FailTask(
                    message,
                    $"The catlet specification {specification.Id} is deployed as catlet {catlet.Id} and cannot be deleted.");
                return;
            }

            var versions = await specificationVersionRepository.ListAsync(
                new CatletSpecificationVersionSpecs.ListBySpecificationId(specification.Id));
            await specificationVersionRepository.DeleteRangeAsync(versions);

            await specificationRepository.DeleteAsync(specification);
        }

        await messaging.CompleteTask(
            message,
            new DestroyResourcesResponse()
            {
                DestroyedResources = [message.Command.Resource],
                DetachedResources = [],
            });
    }
}
