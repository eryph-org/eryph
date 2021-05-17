using System.Threading.Tasks;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.ModuleCore;
using Haipa.Modules.Controller;
using Haipa.Modules.Controller.IdGenerator;
using Haipa.Modules.Controller.Inventory;
using Haipa.Modules.Controller.Operations;
using Haipa.StateDb;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Inventory
{
}


internal class UpdateVMInventoryCommandHandler : UpdateInventoryCommandHandlerBase,
    IHandleMessages<UpdateInventoryCommand>
{
    public UpdateVMInventoryCommandHandler(StateStoreContext stateStoreContext, Id64Generator idGenerator,
        IVirtualMachineMetadataService metadataService, IOperationDispatcher dispatcher,
        IVirtualMachineDataService vmDataService) : base(stateStoreContext, idGenerator, metadataService,
        dispatcher, vmDataService)
    {
    }


    public async Task Handle(UpdateInventoryCommand message)
    {
        var hostMachine = await StateStoreContext.VMHosts
            .FirstOrDefaultAsync(x => x.Name == message.AgentName);

        if (hostMachine == null)
            return;

        await UpdateVMs(message.Inventory, hostMachine);
    }
}