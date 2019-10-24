using System.Threading;
using System.Threading.Tasks;
using Haipa.Messages.Events;
using Haipa.Rebus;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;

namespace Haipa.Modules.Controller.Inventory
{
    public class InventoryModuleService : IHostedService
    {
        private readonly IBus _bus;

        public InventoryModuleService(IBus bus)
        {
            _bus = bus;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {            
            return _bus.Advanced.Topics.Publish($"broadcast_{QueueNames.VMHostAgent}", new InventoryRequestedEvent());
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}