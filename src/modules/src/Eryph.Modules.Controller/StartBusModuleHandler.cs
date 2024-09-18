using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore.Startup;
using Rebus.Bus;

namespace Eryph.Modules.Controller;

public class StartBusModuleHandler(
    IBus bus,
    WorkflowOptions workflowOptions)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await OperationsSetup.SubscribeEvents(bus, workflowOptions);
        await bus.Advanced.Topics.Subscribe("vm_events");
    }
}