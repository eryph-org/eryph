using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.ModuleCore.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity;

/// <summary>
/// Applies the identity-database migrations on startup, before the seeders run. Relational only: on the
/// in-memory provider (test wiring) there are no migrations, so it is a no-op.
/// <para>
/// Registered by the HOST, not the module, so the choice between in-process migration and out-of-band
/// setup is the host's: eryph-zero (SQLite, single process) adds it to own its disposable database;
/// the split-runtime identity host does NOT — its schema is set up out of band (the <c>create-db</c>
/// command in dev, SQL setup scripts in production), exactly like the controller's state database.
/// </para>
/// </summary>
public sealed class MigrateIdentityDbHandler(
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
