using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Haipa.Rebus;
using Rebus.Bus;

namespace Haipa.Modules.VmHostAgent
{
    public class StartBusModuleHandler : IHostedServiceHandler
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