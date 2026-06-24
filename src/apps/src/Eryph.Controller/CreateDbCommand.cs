using System;
using System.Threading.Tasks;
using Eryph.StateDb.MySql;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Controller
{
    /// <summary>
    /// Development setup command: creates the state-database schema in an empty database, then exits.
    /// <code>eryph-controller create-db</code>
    /// The cluster schema is SETUP, not a migration the service applies at startup — in production it is
    /// owned by SQL setup scripts (the idempotent script generated from the EF model). This command is the
    /// dev convenience for that: it fills an empty database from the current model (no migration history),
    /// so a cold cluster can be brought up without the service racing its own schema creation.
    /// </summary>
    internal static class CreateDbCommand
    {
        public const string Verb = "create-db";

        public static async Task<int> RunAsync(string[] args)
        {
            var connectionString = ControllerContainerExtensions.GetStateDbConnectionString();

            var builder = new DbContextOptionsBuilder<MySqlStateStoreContext>();
            new MySqlStateStoreContextConfigurer(connectionString).Configure(builder);
            await using var context = new MySqlStateStoreContext(builder.Options);

            var created = await context.Database.EnsureCreatedAsync();
            Console.WriteLine(created
                ? "State database schema created."
                : "State database already initialised; nothing to do.");
            return 0;
        }
    }
}
