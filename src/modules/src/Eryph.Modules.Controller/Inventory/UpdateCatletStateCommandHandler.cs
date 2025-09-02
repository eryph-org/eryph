using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateCatletStateCommandHandler(
    ITaskMessaging messaging,
    IInventoryLockManager lockManager,
    ICatletDataService vmDataService,
    ILogger logger)
    : IHandleMessages<OperationTask<UpdateCatletStateCommand>>
{
    public async Task Handle(OperationTask<UpdateCatletStateCommand> message)
    {
        await lockManager.AcquireVmLock(message.Command.VmId);

        var catlet = await vmDataService.Get(message.Command.CatletId);
        if (catlet is null)
            return;

        if (catlet.LastSeenState < message.Command.Timestamp)
        {
            catlet.UpTime = message.Command.Status is VmStatus.Stopped ? TimeSpan.Zero : message.Command.UpTime;
            catlet.Status = message.Command.Status.ToCatletStatus();
            catlet.LastSeenState = message.Command.Timestamp;
        }
        else
        {
            logger.LogDebug("Skipping state update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent state information is dated {LastSeen:O}.",
                catlet.Id, message.Command.Timestamp, catlet.LastSeenState);
        }

        await messaging.CompleteTask(message);
    }
}
