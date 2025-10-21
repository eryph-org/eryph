using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DeleteCatletSpecificationCommandHandler(
    IStateStoreRepository<CatletSpecification> repository,
    ITaskMessaging messaging)
    : IHandleMessages<OperationTask<DeleteCatletSpecificationCommand>>
{
    public async Task Handle(OperationTask<DeleteCatletSpecificationCommand> message)
    {
        var specification = await repository.GetByIdAsync(message.Command.SpecificationId);
        if (specification is not null)
        {
            // TODO handle existing catlet
            await repository.DeleteAsync(specification);
        }
        await messaging.CompleteTask(message);
    }
}
