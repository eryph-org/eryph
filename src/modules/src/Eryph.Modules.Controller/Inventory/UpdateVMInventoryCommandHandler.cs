using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateVMInventoryCommandHandler
    : UpdateInventoryCommandHandlerBase,
    IHandleMessages<UpdateInventoryCommand>
{
    private readonly IVMHostMachineDataService _vmHostDataService;

    public UpdateVMInventoryCommandHandler(
        IInventoryLockManager lockManager,
        IVirtualMachineMetadataService metadataService,
        IOperationDispatcher dispatcher,
        IMessageContext messageContext,
        IVirtualMachineDataService vmDataService,
        IVirtualDiskDataService vhdDataService,
        IVMHostMachineDataService vmHostDataService,
        IStateStore stateStore,
        ILogger logger) :
        base(lockManager,
            metadataService,
            dispatcher,
            vmDataService,
            vhdDataService,
            stateStore,
            messageContext,
            logger)
    {
        _vmHostDataService = vmHostDataService;
    }


    public Task Handle(UpdateInventoryCommand message)
    {
        return _vmHostDataService.GetVMHostByAgentName(message.AgentName)
            .IfSomeAsync(hostMachine => UpdateVMs(message.Timestamp, message.Inventory, hostMachine));
    }
}