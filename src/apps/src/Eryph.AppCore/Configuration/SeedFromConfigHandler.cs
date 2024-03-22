using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using LanguageExt;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Configuration
{
    public class SeedFromConfigHandler<TModule> : IHostedServiceHandler where TModule : class
    {
        private readonly IEnumerable<DependencyMetadata<IConfigSeeder<TModule>>> _seeders;
        private readonly Container _container;

        public SeedFromConfigHandler(IEnumerable<DependencyMetadata<IConfigSeeder<TModule>>> seeders, Container container)
        {
            _seeders = seeders;
            this._container = container;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            foreach (var configSeeder in _seeders)
            {
                await using var scope = AsyncScopedLifestyle.BeginScope(_container);

                await configSeeder.GetInstance().Execute(stoppingToken);
            }
        }
    }
}