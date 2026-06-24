using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Seeding;

internal class DefaultTenantSeeder(
    ILogger logger,
    IStateStore stateStore) : IConfigSeeder<ControllerModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        var tenantId = EryphConstants.DefaultTenantId;
        var tenant = await stateStore.For<Tenant>().GetByIdAsync(tenantId, stoppingToken);

        if (tenant == null)
        {
            logger.LogInformation("Default tenant '{tenantId}' not found in state db. Creating tenant record.",
                EryphConstants.DefaultTenantId);

            tenant = new Tenant { Id = tenantId };
            await stateStore.For<Tenant>().AddAsync(tenant, stoppingToken);
            await stateStore.For<Tenant>().SaveChangesAsync(stoppingToken);
        }
    }
}
