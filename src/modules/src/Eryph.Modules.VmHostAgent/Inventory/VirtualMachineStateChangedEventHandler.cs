using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement.Inventory;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Inventory;

internal class VirtualMachineStateChangedEventHandler(IBus bus)
    : IHandleMessages<VirtualMachineStateChangedEvent>
{
    public Task Handle(VirtualMachineStateChangedEvent message) =>
        bus.Advanced.Topics.Publish("vm_events", new VMStateChangedEvent
        {
            VmId = message.VmId,
            Status = InventoryConverter.MapVmInfoStatusToVmStatus(message.State),
            Timestamp = message.Timestamp,
        });
}