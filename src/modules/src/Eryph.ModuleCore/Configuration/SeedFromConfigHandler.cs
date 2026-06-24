using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.ModuleCore.Logging;
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
public class SeedFromConfigHandler<TModule>(
    IEnumerable<DependencyMetadata<IConfigSeeder<TModule>>> seeders,
    Container container)
    : IHostedService
    where TModule : class
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var configSeeder in seeders)
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(container);
            var logger = scope.GetInstance<ILogger<SeedFromConfigHandler<TModule>>>();
            using (_ = logger.BeginWarmupProgressScope())
            {
                logger.LogInformation("Executing config seeder {configSeeder}", configSeeder.ImplementationType.Name);
            }

            await configSeeder.GetInstance().Execute(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
