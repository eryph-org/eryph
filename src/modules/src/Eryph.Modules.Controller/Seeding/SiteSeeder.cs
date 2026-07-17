using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Seeding;

/// <summary>
/// Seeds the default site. Every site bound resource has a foreign key to a site,
/// so this must run before any seeder which recreates resources.
/// </summary>
[UsedImplicitly]
internal class SiteSeeder(
    ILogger logger,
    IStateStore stateStore)
    : IConfigSeeder<ControllerModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        var site = await stateStore.For<Site>().GetByIdAsync(
            EryphConstants.DefaultSiteId, stoppingToken);

        if (site is null)
        {
            logger.LogInformation("Default site '{SiteId}' not found in state db. Creating site record.",
                EryphConstants.DefaultSiteId);

            await stateStore.For<Site>().AddAsync(
                new Site
                {
                    Id = EryphConstants.DefaultSiteId,
                    Name = EryphConstants.DefaultSiteName,
                },
                stoppingToken);
        }

        await stateStore.SaveChangesAsync(stoppingToken);
    }
}
