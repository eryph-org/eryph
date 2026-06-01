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
    // Cancelled on host shutdown so any background work a handler starts (e.g. a retry loop) stops
    // cleanly instead of running on after the bus is disposed. This is the handler's shutdown signal —
    // the token passed to StartAsync only covers startup, not the application lifetime.
    private readonly CancellationTokenSource _stopping = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var logger = scope.GetInstance<ILogger<StartupHandlerService<THandler>>>();
        logger.LogDebug("Executing startup handler {Handler}...", typeof(THandler).Name);
        var handler = ActivatorUtilities.CreateInstance<THandler>(container);
        await handler.ExecuteAsync(_stopping.Token);
        logger.LogDebug("Startup handler {Handler} executed.", typeof(THandler).Name);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        return Task.CompletedTask;
    }
}
