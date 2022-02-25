using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Eryph.Messages.Resources.Machines.Events;
using Eryph.Rebus;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Inventory
{
    public class InventoryHandler : IHostedServiceHandler
    {
        private readonly IBus _bus;

        public InventoryHandler(IBus bus)
        {
            _bus = bus;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _bus.Advanced.Topics.Publish($"broadcast_{QueueNames.VMHostAgent}", new InventoryRequestedEvent());
        }
    }
}