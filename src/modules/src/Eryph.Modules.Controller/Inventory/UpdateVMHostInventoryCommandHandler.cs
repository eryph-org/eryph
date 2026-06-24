using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateVMHostInventoryCommandHandler(
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
        IHandleMessages<UpdateVMHostInventoryCommand>
{
    private readonly IInventoryLockManager _lockManager = lockManager;
    private readonly ICatletDataService _vmDataService = vmDataService;

    public async Task Handle(UpdateVMHostInventoryCommand message)
    {
        var hostInventory = message.HostInventory ?? throw new InvalidOperationException(
            "The host inventory is missing.");
        var hostName = hostInventory.Name ?? throw new InvalidOperationException(
            "The host inventory is missing the host name.");

        var vmHost = await vmHostDataService.GetVMHostByAgentName(hostName)
            .IfNoneAsync(async () => await vmHostDataService.AddNewVMHost(new CatletFarm
            {
                Id = Guid.NewGuid(),
                Name = hostName,
                Project = await FindRequiredProject(EryphConstants.DefaultProjectName, null),
                Environment = EryphConstants.DefaultEnvironmentName,
            }));

        if (IsUpdateOutdated(vmHost, message.Timestamp))
            return;

        var knownVmIds = await _vmDataService.GetAllVmIds(hostName);
        foreach (var vmId in knownVmIds) await _lockManager.AcquireVmLock(vmId);

        var diskInventory = message.DiskInventory ?? [];
        var diskIdentifiers = CollectDiskIdentifiers(diskInventory.ToSeq());
        foreach (var diskIdentifier in diskIdentifiers) await _lockManager.AcquireVhdLock(diskIdentifier);

        foreach (var diskInfo in diskInventory) await AddOrUpdateDisk(vmHost.Name, message.Timestamp, diskInfo);

        var vmInventory = message.VMInventory ?? [];
        await UpdateVms(message.Timestamp, vmInventory, vmHost);

        // The inventory by the host agent should contain all VMs that are present on the host.
        // Hence, we can mark all VMs that are not in the inventory as missing.
        foreach (var missingVmId in knownVmIds.Except(vmInventory.Select(vm => vm.VmId)))
        {
            var catlet = await _vmDataService.GetByVmId(missingVmId);
            if (catlet is null || catlet.LastSeenState > message.Timestamp)
                continue;

            catlet.Status = CatletStatus.Missing;
            catlet.LastSeenState = message.Timestamp;
            catlet.UpTime = TimeSpan.Zero;
        }

        await CheckDisks(message.Timestamp, vmHost.Name);

        vmHost.LastInventory = message.Timestamp;
    }
}
