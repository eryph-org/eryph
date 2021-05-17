using System.Threading.Tasks;
using Haipa.Messages.Resources.Machines.Events;
using Haipa.Modules.VmHostAgent.Inventory;
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