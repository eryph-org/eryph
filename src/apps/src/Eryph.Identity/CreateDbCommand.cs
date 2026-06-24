using System;
using System.Threading.Tasks;
using Eryph.IdentityDb.MySql;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Identity
{
    /// <summary>
    /// Development setup command: creates the identity-database schema in an empty database, then exits.
    /// <code>eryph-identity create-db</code>
    /// The cluster schema is SETUP, not a migration the service applies at startup — in production it is
    /// owned by SQL setup scripts (the idempotent script generated from the EF model). This command is the
    /// dev convenience for that: it fills an empty database from the current model (no migration history),
    /// mirroring the controller's <c>create-db</c> for the state database.
    /// </summary>
    internal static class CreateDbCommand
    {
        public const string Verb = "create-db";

        public static async Task<int> RunAsync(string[] args)
        {
            var connectionString = IdentityContainerExtensions.GetIdentityDbConnectionString();

            var builder = new DbContextOptionsBuilder<MySqlIdentityDbContext>();
            new MySqlIdentityDbContextConfigurer(connectionString).Configure(builder);
            await using var context = new MySqlIdentityDbContext(builder.Options);

            var created = await context.Database.EnsureCreatedAsync();
            Console.WriteLine(created
                ? "Identity database schema created."
                : "Identity database already initialised; nothing to do.");
            return 0;
        }
    }
}
