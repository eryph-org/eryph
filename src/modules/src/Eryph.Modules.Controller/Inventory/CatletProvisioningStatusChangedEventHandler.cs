using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory;

/// <summary>
/// Persists the provisioning status reported by the agent's provisioning monitor
/// onto the catlet. Mirrors <see cref="CatletStateChangedEventHandler"/>: the
/// catlet is resolved by its VM id and the update is guarded by a monotonic
/// observation timestamp so out-of-order reports cannot regress the status.
/// </summary>
[UsedImplicitly]
internal class CatletProvisioningStatusChangedEventHandler(
    IInventoryLockManager lockManager,
    ICatletDataService vmDataService,
    ILogger logger)
    : IHandleMessages<CatletProvisioningStatusChangedEvent>
{
    public async Task Handle(CatletProvisioningStatusChangedEvent message)
    {
        await lockManager.AcquireVmLock(message.VmId);

        var catlet = await vmDataService.GetByVmId(message.VmId);
        if (catlet is null)
            return;

        // Never regress a known status with an unknown one.
        if (message.Status is ProvisioningStatus.Unknown)
            return;

        if (catlet.LastSeenProvisioningStatus < message.Timestamp)
        {
            catlet.ProvisioningStatus = message.Status.ToCatletProvisioningStatus();
            catlet.LastSeenProvisioningStatus = message.Timestamp;
        }
        else
        {
            logger.LogDebug(
                "Skipping provisioning status update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent provisioning status information is dated {LastSeen:O}.",
                catlet.Id, message.Timestamp, catlet.LastSeenProvisioningStatus);
        }
    }
}
