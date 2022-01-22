using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Eryph.Messages;
using Rebus.Bus;

namespace Eryph.Modules.Controller
{
    public class StartBusModuleHandler : IHostedServiceHandler
    {
        private readonly IBus _bus;

        public StartBusModuleHandler(IBus bus)
        {
            _bus = bus;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            foreach (var type in MessageTypes.BySubscriber(MessageSubscriber.Controllers))
                await _bus.Subscribe(type).ConfigureAwait(false);
        }
    }
}