using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement.Inventory;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    internal class VirtualMachineStateChangedEventHandler : IHandleMessages<VirtualMachineStateChangedEvent>
    {
        private readonly IBus _bus;

        public VirtualMachineStateChangedEventHandler(IBus bus)
        {
            _bus = bus;
        }

        public Task Handle(VirtualMachineStateChangedEvent message)
        {
            return _bus.Advanced.Topics.Publish("vm_events",new VMStateChangedEvent
            {
                VmId = message.VmId,
                Status = InventoryConverter.MapVmInfoStatusToVmStatus(message.State),
                TimeStamp = message.TimeStamp,
            });
        }
    }
}