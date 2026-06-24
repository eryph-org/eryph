using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Disks;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateDiskInventoryCommandHandler(
    IInventoryLockManager lockManager,
    ICatletMetadataService metadataService,
    IOperationDispatcher dispatcher,
    IMessageContext messageContext,
    ICatletDataService vmDataService,
    IVMHostMachineDataService vmHostDataService,
    IStateStore stateStore,
    ILogger logger)
    : UpdateInventoryCommandHandlerBase(
            lockManager,
            metadataService,
            dispatcher,
            vmDataService,
            stateStore,
            messageContext,
            logger),
        IHandleMessages<UpdateDiskInventoryCommand>
{
    private readonly IInventoryLockManager _lockManager = lockManager;

    public async Task Handle(UpdateDiskInventoryCommand message)
    {
        var agentName = message.AgentName ?? throw new InvalidOperationException("AgentName is required");
        var inventory = message.Inventory ?? throw new InvalidOperationException("Inventory is required");

        var vmHost = await vmHostDataService.GetVMHostByAgentName(agentName);
        if (vmHost.IsNone || IsUpdateOutdated(vmHost.ValueUnsafe(), message.Timestamp))
            return;

        var diskIdentifiers = CollectDiskIdentifiers(inventory.ToSeq());
        foreach (var diskIdentifier in diskIdentifiers) await _lockManager.AcquireVhdLock(diskIdentifier);

        foreach (var diskInfo in inventory)
            await AddOrUpdateDisk(agentName, message.Timestamp, diskInfo);

        await CheckDisks(message.Timestamp, agentName);
    }
}
