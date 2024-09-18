using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ModuleCore.Startup;

internal class StartupHandlerService<THandler>(Container container) : IHostedService
    where THandler : class, IStartupHandler
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var logger = scope.GetInstance<ILogger<StartupHandlerService<THandler>>>();
        logger.LogDebug("Executing startup handler {Handler}...", typeof(THandler).Name);
        var handler = ActivatorUtilities.CreateInstance<THandler>(container);
        await handler.ExecuteAsync(cancellationToken);
        logger.LogDebug("Startup handler {Handler} executed.", typeof(THandler).Name);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
