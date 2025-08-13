using Dbosoft.Rebus.Operations;
using Eryph.Messages;
using Eryph.Messages.Resources.Catlets.Commands;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
