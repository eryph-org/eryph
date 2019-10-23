using System.Threading;
using System.Threading.Tasks;
using Haipa.Messages;
using Rebus.Bus;

namespace Haipa.Modules.Controller
{
    public class StartBusModuleHandler : IModuleHandler
    {
        private readonly IBus _bus;

        public StartBusModuleHandler(IBus bus)
        {
            _bus = bus;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            foreach (var type in MessageTypes.BySubscriber(MessageSubscriber.Controllers))
            {
                await _bus.Subscribe(type).ConfigureAwait(false);
            }

            
        }
    }
}