using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ModuleCore.Startup;

internal class StartupHandlerService<THandler>(Container container) : IHostedService, IDisposable
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
        // Resolve the handler from the active SimpleInjector scope (it is registered via
        // AddStartupHandler). ActivatorUtilities.CreateInstance would bypass the container's scope,
        // so scoped dependencies could be resolved from the wrong scope and never disposed.
        var handler = scope.GetInstance<THandler>();
        // Cancel on startup abort too, not only on shutdown: while StartAsync runs, a cancelled startup
        // token should stop the handler as well. (After StartAsync returns, shutdown is signalled via
        // StopAsync; background work keeps observing _stopping.Token.)
        using var registration = cancellationToken.Register(
            static state => ((CancellationTokenSource)state!).Cancel(), _stopping);
        await handler.ExecuteAsync(_stopping.Token);
        logger.LogDebug("Startup handler {Handler} executed.", typeof(THandler).Name);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        return Task.CompletedTask;
    }

    // Disposed by the host after StopAsync (background work has observed the cancellation by then);
    // releases the CancellationTokenSource's wait handle so it does not leak across host start/stop.
    public void Dispose() => _stopping.Dispose();
}
