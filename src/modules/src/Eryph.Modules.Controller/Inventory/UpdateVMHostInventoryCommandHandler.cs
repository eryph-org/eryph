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
    IVirtualMachineMetadataService metadataService,
    IOperationDispatcher dispatcher,
    IMessageContext messageContext,
    IVirtualMachineDataService vmDataService,
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

    public async Task Handle(UpdateVMHostInventoryCommand message)
    {
        var vmHost = await vmHostDataService.GetVMHostByHardwareId(message.HostInventory.HardwareId)
            .IfNoneAsync(async () => await vmHostDataService.AddNewVMHost(new CatletFarm
            {
                Id = Guid.NewGuid(),
                Name = message.HostInventory.Name,
                HardwareId = message.HostInventory.HardwareId,
                Project = await FindRequiredProject(EryphConstants.DefaultProjectName, null),
                Environment = EryphConstants.DefaultEnvironmentName,
            }));

        if (IsUpdateOutdated(vmHost, message.Timestamp))
            return;

        var diskIdentifiers = CollectDiskIdentifiers(message.DiskInventory.ToSeq());
        foreach (var diskIdentifier in diskIdentifiers)
        {
            await _lockManager.AcquireVhdLock(diskIdentifier);
        }

        foreach (var diskInfo in message.DiskInventory)
        {
            await AddOrUpdateDisk(vmHost.Name, message.Timestamp, diskInfo);
        }

        await UpdateVMs(message.Timestamp, message.VMInventory, vmHost);

        await CheckDisks(message.Timestamp, vmHost.Name);

        vmHost.LastInventory = message.Timestamp;
    }
}
