using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Startup;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Controller;

/// <summary>
/// Applies the state-database migrations on startup. In eryph-zero this happens in a
/// separate warmup process; the standalone Controller owns its database, so it migrates
/// in-process before the bus and message handlers start.
/// </summary>
internal sealed class MigrateStateDbHandler(
    StateStoreContext context,
    ILogger<MigrateStateDbHandler> logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying state database migrations...");
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("State database is up to date.");
    }
}
