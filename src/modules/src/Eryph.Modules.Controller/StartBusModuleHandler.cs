using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Rebus.Operations;
using Rebus.Bus;

namespace Eryph.Modules.Controller
{
    public class StartBusModuleHandler : IHostedServiceHandler
    {
        private readonly IBus _bus;
        private readonly WorkflowOptions _workflowOptions;

        public StartBusModuleHandler(IBus bus, WorkflowOptions workflowOptions)
        {
            _bus = bus;
            _workflowOptions = workflowOptions;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return OperationsSetup.SubscribeEvents(_bus, _workflowOptions);
        }
    }
}