using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eryph.Runtime.Zero
{
    /// <summary>
    ///     This type is required for EfCore migrations.
    /// </summary>
    /// <remarks>
    ///     To add a migration the ad--migration commands requires a connection to the database. This type will be used by the
    ///     add-migration command
    ///     to configure EfCore context without the normally required container setup that is executed on application startup.
    ///     To use the migrations set the environment variable MYSQL_CONNECTIONSTRING to the database connection string.
    ///     Then call the migration as follows:
    ///     Add-Migration -StartupProject Eryph-zero -Project Eryph.StateDb -Name [name_of_migration]
    /// </remarks>
    [UsedImplicitly]
    public class DesignTimeStateStoreContextFactory : IDesignTimeDbContextFactory<StateStoreContext>
    {
        public StateStoreContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StateStoreContext>();
            optionsBuilder.UseSqlite();

            return new StateStoreContext(optionsBuilder.Options);
        }
    }
}