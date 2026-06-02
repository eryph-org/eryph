using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.ModuleCore.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity;

/// <summary>
/// Applies the identity-database migrations on startup, before the seeders run. Relational only: on the
/// in-memory provider (test wiring) there are no migrations, so it is a no-op. Both the standalone
/// identity host (MariaDB) and eryph-zero (SQLite) own their database and migrate it in-process.
/// </summary>
internal sealed class MigrateIdentityDbHandler(
    IdentityDbContext context,
    ILogger<MigrateIdentityDbHandler> logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
            return;

        logger.LogInformation("Applying identity database migrations...");
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Identity database is up to date.");
    }
}
