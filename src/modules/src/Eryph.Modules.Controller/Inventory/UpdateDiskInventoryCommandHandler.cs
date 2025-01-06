using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Disks;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateDiskInventoryCommandHandler(
    IInventoryLockManager lockManager,
    IVirtualMachineMetadataService metadataService,
    IOperationDispatcher dispatcher,
    IMessageContext messageContext,
    IVirtualMachineDataService vmDataService,
    IVirtualDiskDataService vhdDataService,
    IStateStore stateStore,
    ILogger logger)
    : UpdateInventoryCommandHandlerBase(
            lockManager,
            metadataService,
            dispatcher,
            vmDataService,
            vhdDataService,
            stateStore,
            messageContext,
            logger),
        IHandleMessages<UpdateDiskInventoryCommand>
{
    private readonly IInventoryLockManager _lockManager = lockManager;

    public async Task Handle(UpdateDiskInventoryCommand message)
    {
        var diskIdentifiers = CollectDiskIdentifiers(message.Inventory.ToSeq());
        foreach (var diskIdentifier in diskIdentifiers)
        {
            await _lockManager.AcquireVhdLock(diskIdentifier);
        }

        foreach (var diskInfo in message.Inventory)
        {
            await AddOrUpdateDisk(message.AgentName, message.Timestamp, diskInfo);
        }

        await CheckDisks(message.Timestamp, message.AgentName);
    }
}
