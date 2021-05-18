using System.Threading.Tasks;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.ModuleCore;
using Haipa.Modules.Controller.DataServices;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Inventory
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


        public Task Handle(UpdateInventoryCommand message)
        {
            return _vmHostDataService.GetVMHostByAgentName(message.AgentName)
                .IfSomeAsync(hostMachine => UpdateVMs(message.Inventory, hostMachine));
        }
    }
}