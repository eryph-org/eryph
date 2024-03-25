using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;

namespace Eryph.ZeroState
{
    public interface IZeroStateSeeder
    {
        public Task SeedAsync(CancellationToken stoppingToken = default);
    }

    internal class ZeroStateSeederBase : IZeroStateSeeder
    {
        public Task SeedAsync(CancellationToken stoppingToken = default)
        {
            // TODO create backup of seeded config file
            throw new NotImplementedException();
        }
    }
}
