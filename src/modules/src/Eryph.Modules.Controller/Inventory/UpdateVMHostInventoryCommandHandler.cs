using System;
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

namespace Eryph.Modules.Controller.Inventory
{
    [UsedImplicitly]
    internal class UpdateVMHostInventoryCommandHandler : UpdateInventoryCommandHandlerBase,
        IHandleMessages<UpdateVMHostInventoryCommand>
    {
        private readonly IVMHostMachineDataService _vmHostDataService;

        public UpdateVMHostInventoryCommandHandler(
            IVirtualMachineMetadataService metadataService, 
            IOperationDispatcher dispatcher,
            IMessageContext messageContext,
            IVirtualMachineDataService vmDataService,
            IVirtualDiskDataService vhdDataService, 
            IVMHostMachineDataService vmHostDataService, IStateStore stateStore) : 
            base(metadataService, dispatcher, vmDataService, vhdDataService, stateStore, messageContext)
        {
            _vmHostDataService = vmHostDataService;
        }

        public async Task Handle(UpdateVMHostInventoryCommand message)
        {
            var newMachineState = await
                _vmHostDataService.GetVMHostByHardwareId(message.HostInventory.HardwareId).IfNoneAsync(
                async () => new CatletFarm
                {
                    Id = Guid.NewGuid(),
                    Name = message.HostInventory.Name,
                    HardwareId = message.HostInventory.HardwareId,
                    Project = await FindRequiredProject(message.TenantId, "default")
                });

            var existingMachine = await _vmHostDataService.GetVMHostByHardwareId(message.HostInventory.HardwareId)
                .IfNoneAsync(() => _vmHostDataService.AddNewVMHost(newMachineState));

            await UpdateVMs(message.TenantId, message.VMInventory, existingMachine);

        }
    }
}