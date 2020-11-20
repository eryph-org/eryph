using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Modules.Controller.IdGenerator;
using Haipa.Modules.Controller.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Inventory
{
    internal class UpdateVMHostInventoryCommandHandler : UpdateInventoryCommandHandlerBase, IHandleMessages<UpdateVMHostInventoryCommand>
    {

        public UpdateVMHostInventoryCommandHandler(StateStoreContext stateStoreContext, Id64Generator idGenerator, IVirtualMachineMetadataService metadataService, IOperationTaskDispatcher taskDispatcher, IVirtualMachineDataService vmDataService) : base(stateStoreContext, idGenerator, metadataService, taskDispatcher, vmDataService)
        {
        }

        public async Task Handle(UpdateVMHostInventoryCommand message)
        {
            var newMachine =
                await StateStoreContext.VMHosts.FirstOrDefaultAsync(x => x.Name == message.HostInventory.Name)
                ?? new VMHostMachine
                {
                    Id = IdGenerator.GenerateId(),
                    AgentName = message.HostInventory.Name,
                    Name = message.HostInventory.Name,
                };

            newMachine.Networks = message.HostInventory.Networks.ToMachineNetwork(newMachine.Id).ToList();
            newMachine.Status = MachineStatus.Running;

            var existingMachine =
                await StateStoreContext.VMHosts.FirstOrDefaultAsync(x => x.Name == message.HostInventory.Name);

            if (existingMachine != null)
            {
                MergeMachineNetworks(newMachine, existingMachine);
            }
            else
            {
                existingMachine = newMachine;
                await StateStoreContext.AddAsync(newMachine);
            }

            await UpdateVMs(message.VMInventory, existingMachine);


        }

    }
}