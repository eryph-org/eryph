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
    /// <para>
    /// EnsureCreated, NOT Migrate, on purpose: a dev database is created fresh and recreated on schema
    /// change (the harness uses throwaway databases), so it never carries migration history. Do not point
    /// migration-based tooling (or the idempotent SQL script) at a database created by this command — a
    /// given database is set up by exactly one mechanism: this command in dev, the SQL script in production.
    /// </para>
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
