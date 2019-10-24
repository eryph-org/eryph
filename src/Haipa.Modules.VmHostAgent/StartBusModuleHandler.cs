using System.Threading;
using System.Threading.Tasks;
using Haipa.Rebus;
using Rebus.Bus;

namespace Haipa.Modules.VmHostAgent
{
    public class StartBusModuleHandler : IModuleHandler
    {
        private readonly IBus _bus;

        public StartBusModuleHandler(IBus bus)
        {
            _bus = bus;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _bus.Advanced.Topics.Subscribe($"broadcast_{QueueNames.VMHostAgent}");
        }
    }
}