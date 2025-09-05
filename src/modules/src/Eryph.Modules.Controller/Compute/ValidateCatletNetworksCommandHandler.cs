using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Compute;

internal class ValidateCatletNetworksCommandHandler(
    ITaskMessaging messaging)
    : IHandleMessages<OperationTask<ValidateCatletNetworksCommand>>
{
    public async Task Handle(OperationTask<ValidateCatletNetworksCommand> message)
    {
        // TODO Implement validation logic
        await messaging.CompleteTask(message);
    }
}
