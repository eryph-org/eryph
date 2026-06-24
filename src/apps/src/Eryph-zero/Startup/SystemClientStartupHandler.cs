using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Startup;
using Eryph.Runtime.Zero.Configuration.Clients;

namespace Eryph.Runtime.Zero.Startup;

internal class SystemClientStartupHandler(
    ISystemClientGenerator systemClientGenerator)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await systemClientGenerator.EnsureSystemClient();
    }
}
