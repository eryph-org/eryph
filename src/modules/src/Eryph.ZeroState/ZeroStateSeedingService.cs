using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ZeroState
{
    internal class ZeroStateSeedingService : IHostedService
    {
        private readonly IEnumerable<DependencyMetadata<IZeroStateSeeder>> _seeders;
        private readonly Container _container;

        public ZeroStateSeedingService(
            Container container,
            IEnumerable<DependencyMetadata<IZeroStateSeeder>> seeders)
        {
            _container = container;
            _seeders = seeders;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var configSeeder in _seeders)
            {
                await using var scope = AsyncScopedLifestyle.BeginScope(_container);

                await configSeeder.GetInstance().SeedAsync(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
