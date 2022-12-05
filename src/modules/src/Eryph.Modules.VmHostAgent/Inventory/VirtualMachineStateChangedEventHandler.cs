using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data;
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
            return _bus.Publish(new VMStateChangedEvent
            {
                VmId = message.VmId,
                Status = InventoryConverter.MapVmInfoStatusToVmStatus(message.State)
            });
        }
    }
}