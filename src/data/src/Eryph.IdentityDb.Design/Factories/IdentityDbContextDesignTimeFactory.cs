using System;
using Eryph.IdentityDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microting.EntityFrameworkCore.MySql.Infrastructure;

namespace Eryph.IdentityDb.Design.Factories;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to generate the identity-database migrations. The
/// identity model is a single <see cref="IdentityDbContext"/> shared by both providers, so a single
/// factory selects the provider from the first CLI argument (after <c>--</c>): <c>sqlite</c> for the
/// eryph-zero store, anything else (default) for MariaDB. Each provider's migrations are kept in its own
/// assembly via <c>MigrationsAssembly</c>. The same OpenIddict model extension applied at runtime
/// (<see cref="IdentityDbModel.ApplyOpenIddict"/>) is applied here, or the migration would omit the
/// OpenIddict tables. No connection string is needed for model building.
/// </summary>
/// <remarks>
/// Generate with, e.g.:
/// <code>
/// dotnet ef migrations add InitialCreate -p src/data/src/Eryph.IdentityDb.MySql  -s src/data/src/Eryph.IdentityDb.Design -- mysql
/// dotnet ef migrations add InitialCreate -p src/data/src/Eryph.IdentityDb.Sqlite -s src/data/src/Eryph.IdentityDb.Design -- sqlite
/// </code>
/// </remarks>
public class IdentityDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var provider = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "mysql";
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();

        if (provider == "sqlite")
        {
            optionsBuilder.UseSqlite(b => b.MigrationsAssembly("Eryph.IdentityDb.Sqlite"));
        }
        else
        {
            optionsBuilder.UseMySql(
                ServerVersion.Create(10, 11, 0, ServerType.MariaDb),
                b => b.MigrationsAssembly("Eryph.IdentityDb.MySql"));
        }

        IdentityDbModel.ApplyOpenIddict(optionsBuilder);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
