using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState
{
    internal class ZeroStateDefaultTenantSeeder : IZeroStateSeeder
    {
        private readonly IStateStore _stateStore;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public ZeroStateDefaultTenantSeeder(
            IStateStore stateStore,
            IFileSystem fileSystem,
            ILogger logger)
        {
            _stateStore = stateStore;
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken stoppingToken = default)
        {
            var tenantId = EryphConstants.DefaultTenantId;
            var tenant = await _stateStore.For<Tenant>().GetByIdAsync(tenantId, stoppingToken);
            if (tenant is not null)
                return;

            _logger.LogInformation("Default tenant '{tenantId}' not found in state db. Creating tenant record.", tenantId);

            tenant = new Tenant { Id = tenantId };
            await _stateStore.For<Tenant>().AddAsync(tenant, stoppingToken);
            await _stateStore.SaveChangesAsync(stoppingToken);
        }
    }
}
