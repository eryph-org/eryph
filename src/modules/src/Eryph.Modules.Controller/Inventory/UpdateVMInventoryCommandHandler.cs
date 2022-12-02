using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory
{

    [UsedImplicitly]
    internal class UpdateVMInventoryCommandHandler : UpdateInventoryCommandHandlerBase,
        IHandleMessages<UpdateInventoryCommand>
    {
        private readonly IVMHostMachineDataService _vmHostDataService;

        public UpdateVMInventoryCommandHandler(
            IVirtualMachineMetadataService metadataService,
            IOperationDispatcher dispatcher,
            IVirtualMachineDataService vmDataService,
            IVirtualDiskDataService vhdDataService, IVMHostMachineDataService vmHostDataService) :
            base(metadataService, dispatcher, vmDataService, vhdDataService)
        {
            _vmHostDataService = vmHostDataService;
        }


        public async Task Handle(UpdateInventoryCommand message)
        {
            //return _vmHostDataService.GetVMHostByAgentName(message.AgentName)
            //    .IfSomeAsync(hostMachine => UpdateVMs(message.Inventory, hostMachine));
        }
    }
}