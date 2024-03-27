using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;

namespace Eryph.ZeroState
{
    public interface IZeroStateSeeder 
    {
        public Task SeedAsync(CancellationToken stoppingToken = default);
    }
}
