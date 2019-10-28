using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Events;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.VmManagement;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
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
            return _bus.Publish(new MachineStateChangedEvent
            {
                MachineId = message.VmId,
                Status = InventoryConverter.MapVmInfoStatusToVmStatus(message.State)
            });
        }
    }
}