using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement.Inventory;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Inventory;

internal class VirtualMachineStateChangedEventHandler(
    IBus bus,
    WorkflowOptions workflowOptions)
    : IHandleMessages<VirtualMachineStateChangedEvent>
{
    public Task Handle(VirtualMachineStateChangedEvent message) =>
        bus.SendWorkflowEvent(workflowOptions, new CatletStateChangedEvent
        {
            VmId = message.VmId,
            Status = InventoryConverter.MapVmInfoStatusToVmStatus(message.State),
            UpTime = message.UpTime,
            Timestamp = message.Timestamp,
        });
}
