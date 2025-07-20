using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Startup;
using Eryph.Rebus;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent;

public class StartBusModuleHandler(IBus bus) : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await bus.Advanced.Topics.Subscribe($"broadcast_{QueueNames.VMHostAgent}");
    }
}
