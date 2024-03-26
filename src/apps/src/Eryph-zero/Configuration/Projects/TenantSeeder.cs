using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.Modules.Controller;
using Eryph.StateDb.Model;
using Eryph.StateDb;
using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Eryph.Runtime.Zero.Configuration.Projects
{
    internal class TenantSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly ILogger _logger;
        private readonly IStateStore _stateStore;

        public TenantSeeder(
            ILogger logger,
            IStateStore stateStore)
        {
            _logger = logger;
            _stateStore = stateStore;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            var tenantId = EryphConstants.DefaultTenantId;
            var tenant = await _stateStore.For<Tenant>().GetByIdAsync(tenantId, stoppingToken);

            if (tenant == null)
            {
                _logger.LogInformation("Default tenant '{tenantId}' not found in state db. Creating tenant record.", EryphConstants.DefaultTenantId);

                tenant = new Tenant { Id = tenantId };
                await _stateStore.For<Tenant>().AddAsync(tenant, stoppingToken);
                await _stateStore.For<Tenant>().SaveChangesAsync(stoppingToken);
            }
        }
    }
}
