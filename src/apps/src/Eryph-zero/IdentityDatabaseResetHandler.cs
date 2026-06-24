using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.ModuleCore.Logging;
using Eryph.ModuleCore.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero;

/// <summary>
/// Migrates the identity database during warmup, mirroring <see cref="DatabaseResetHandler"/> for the
/// state store. The identity store is disposable in eryph-zero: its durable content (clients, scopes,
/// redeemed enrollment tokens) is rebuilt from the on-disk config mirror by the identity module's
/// seeders. So when migrations are missing the database is recreated rather than upgraded in place.
/// </summary>
internal class IdentityDatabaseResetHandler(
    ILogger logger,
    IdentityDbContext identityDbContext)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var database = identityDbContext.Database;
        var pendingMigrations = await database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            using (_ = logger.BeginWarmupProgressScope())
            {
                logger.LogInformation(
                    "The identity database is missing migrations. Going to recreate the identity database...");
            }

            await database.EnsureDeletedAsync(cancellationToken);
            await database.MigrateAsync(cancellationToken);
        }
    }
}
