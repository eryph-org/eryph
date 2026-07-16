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
    IComponentRegistry componentRegistry,
    IStateStore stateStore,
    ISiteResolver siteResolver,
    ILogger logger)
    : UpdateInventoryCommandHandlerBase(
            lockManager,
            metadataService,
            dispatcher,
            vmDataService,
            stateStore,
            messageContext,
            siteResolver,
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

        // Where a host is, is decided by its registration, not pinned like a catlet's site: a host
        // IS wherever the operator says it is, and moving one is an assignment rather than a
        // migration. So it is read on every round, not only when the record is created — otherwise a
        // host inventoried before it registered would keep the default site forever, and everything
        // first recorded on it (disks, discovered catlets) would be pinned to the wrong place.
        var siteId = componentRegistry.GetHostAgents()
            .Find(agent => string.Equals(agent.AgentName, hostName, StringComparison.OrdinalIgnoreCase))
            .Map(agent => agent.SiteId)
            .IfNone(EryphConstants.DefaultSiteId);

        var vmHost = await vmHostDataService.GetVMHostByAgentName(hostName)
            .IfNoneAsync(async () => await vmHostDataService.AddNewVMHost(new CatletFarm
            {
                Id = Guid.NewGuid(),
                Name = hostName,
                Project = await FindRequiredProject(EryphConstants.DefaultProjectName, null),
                // A host is not in an environment. It keeps the default one because every resource has
                // one, and it is what scopes where the host's own global resources land.
                Environment = EryphConstants.DefaultEnvironmentName,
                SiteId = siteId,
            }));

        if (IsUpdateOutdated(vmHost, message.Timestamp))
            return;

        if (vmHost.SiteId != siteId)
        {
            logger.LogInformation(
                "Host {HostName} moved to another site. Resources recorded on it from now on are "
                + "located there; the ones already recorded keep their site.", hostName);
            vmHost.SiteId = siteId;
        }

        var knownVmIds = await _vmDataService.GetAllVmIds(hostName);
        foreach (var vmId in knownVmIds) await _lockManager.AcquireVmLock(vmId);

        var diskInventory = message.DiskInventory ?? [];
        var diskIdentifiers = CollectDiskIdentifiers(diskInventory.ToSeq());
        foreach (var diskIdentifier in diskIdentifiers) await _lockManager.AcquireVhdLock(diskIdentifier);

        foreach (var diskInfo in diskInventory)
            await AddOrUpdateDisk(vmHost.Name, message.Timestamp, diskInfo);

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
