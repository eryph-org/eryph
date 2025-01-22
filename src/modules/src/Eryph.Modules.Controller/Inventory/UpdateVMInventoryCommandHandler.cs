using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateVMInventoryCommandHandler(
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
        IHandleMessages<UpdateInventoryCommand>
{
    public async Task Handle(UpdateInventoryCommand message)
    {
        var vmHost = await vmHostDataService.GetVMHostByAgentName(message.AgentName);
        if (vmHost.IsNone || IsUpdateOutdated(vmHost.ValueUnsafe(), message.Timestamp))
            return;
        
        await UpdateVMs(message.Timestamp, [message.Inventory], vmHost.ValueUnsafe());
    }
}
