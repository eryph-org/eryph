using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using LanguageExt;

namespace Eryph.Configuration
{
    public class SeedFromConfigHandler<TModule> : IHostedServiceHandler where TModule : class
    {
        private readonly IEnumerable<IConfigSeeder<TModule>> _seeders;

        public SeedFromConfigHandler(IEnumerable<IConfigSeeder<TModule>> seeders)
        {
            _seeders = seeders;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _seeders.Map(s => s.Execute(stoppingToken).ToUnit()).Traverse(l => l);
        }
    }
}