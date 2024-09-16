using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Startup;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero;

internal class DatabaseResetHandler(
    ILogger logger,
    StateStoreContext stateStoreContext)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var database = stateStoreContext.Database;
        var pendingMigrations = await database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            logger.LogInformation("The state database is missing migrations. Going to reseed the state database...");
            await database.EnsureDeletedAsync(cancellationToken);
            await database.MigrateAsync(cancellationToken);
        }
    }
}
