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
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateVMHostInventoryCommandHandler(
    IDistributedLockManager lockManager,
    IVirtualMachineMetadataService metadataService,
    IOperationDispatcher dispatcher,
    IMessageContext messageContext,
    IVirtualMachineDataService vmDataService,
    IVirtualDiskDataService vhdDataService,
    IVMHostMachineDataService vmHostDataService,
    IStateStore stateStore)
    : UpdateInventoryCommandHandlerBase(
            lockManager,
            metadataService,
            dispatcher,
            vmDataService,
            vhdDataService,
            stateStore,
            messageContext),
        IHandleMessages<UpdateVMHostInventoryCommand>
{
    public async Task Handle(UpdateVMHostInventoryCommand message)
    {
        var newMachineState = await
            vmHostDataService.GetVMHostByHardwareId(message.HostInventory.HardwareId).IfNoneAsync(
                async () => new CatletFarm
                {
                    Id = Guid.NewGuid(),
                    Name = message.HostInventory.Name,
                    HardwareId = message.HostInventory.HardwareId,
                    Project = await FindRequiredProject(EryphConstants.DefaultProjectName, null),
                    Environment = EryphConstants.DefaultEnvironmentName,
                });

        var existingMachine = await vmHostDataService.GetVMHostByHardwareId(message.HostInventory.HardwareId)
            .IfNoneAsync(() => vmHostDataService.AddNewVMHost(newMachineState));

        await UpdateVMs(message.Timestamp, message.VMInventory, existingMachine);
    }
}
