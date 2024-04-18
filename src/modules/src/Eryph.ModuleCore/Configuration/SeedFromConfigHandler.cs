using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ModuleCore.Configuration;

/// <summary>
/// This handler is responsible for running code which seeds initial data
/// during startup.
/// </summary>
/// <remarks>
/// This handler intentionally implements <see cref="IHostedService"/> as
/// the startup should wait for the seeding to complete before continuing.
/// </remarks>
public class SeedFromConfigHandler<TModule> : IHostedService where TModule : class
{
    private readonly IEnumerable<DependencyMetadata<IConfigSeeder<TModule>>> _seeders;
    private readonly Container _container;

    public SeedFromConfigHandler(
        IEnumerable<DependencyMetadata<IConfigSeeder<TModule>>> seeders,
        Container container)
    {
        _seeders = seeders;
        _container = container;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var configSeeder in _seeders)
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_container);
            var logger = scope.GetInstance<ILogger<SeedFromConfigHandler<TModule>>>();
            logger.LogInformation("Executing config seeder {configSeeder}", configSeeder.ImplementationType.Name);

            await configSeeder.GetInstance().Execute(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
