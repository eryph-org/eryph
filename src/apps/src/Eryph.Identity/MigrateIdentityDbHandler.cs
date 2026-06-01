using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.ModuleCore.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Identity;

/// <summary>
/// Applies the identity-database migrations on startup. In eryph-zero the identity store is in-memory
/// and never migrated; the standalone identity host owns its MariaDB database, so it migrates
/// in-process before the system-client / component-CA seeders touch it.
/// </summary>
internal sealed class MigrateIdentityDbHandler(
    IdentityDbContext context,
    ILogger<MigrateIdentityDbHandler> logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying identity database migrations...");
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Identity database is up to date.");
    }
}
