using Eryph.IdentityDb.Sqlite;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eryph.IdentityDb.Design.Factories;

/// <summary>
/// Design-time factory for the eryph-zero SQLite identity store. The OpenIddict model extension applied at
/// runtime (<see cref="IdentityDbModel.ApplyOpenIddict"/>) must be applied here too, or a generated
/// migration would omit every OpenIddict table. Migrations live in the <c>Eryph.IdentityDb.Sqlite</c>
/// assembly (this context's own assembly), so no connection string or MigrationsAssembly is needed.
/// </summary>
/// <remarks>
/// Generate with:
/// <code>
/// dotnet ef migrations add InitialCreate -p src/data/src/Eryph.IdentityDb.Sqlite -s src/data/src/Eryph.IdentityDb.Design -c SqliteIdentityDbContext
/// </code>
/// </remarks>
[UsedImplicitly]
public class SqliteIdentityDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<SqliteIdentityDbContext>
{
    public SqliteIdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteIdentityDbContext>();
        optionsBuilder.UseSqlite();
        IdentityDbModel.ApplyOpenIddict(optionsBuilder);

        return new SqliteIdentityDbContext(optionsBuilder.Options);
    }
}
