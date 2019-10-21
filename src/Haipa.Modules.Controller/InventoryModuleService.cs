using System.Threading;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Events;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;

namespace Haipa.Modules.Controller
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
            return _bus.Advanced.Topics.Publish("agent.all", new InventoryRequestedEvent());
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}