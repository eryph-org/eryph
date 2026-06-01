using Eryph.IdentityDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microting.EntityFrameworkCore.MySql.Infrastructure;

namespace Eryph.IdentityDb.Design.Factories;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to generate the MariaDB migrations for the identity
/// database. It must apply the same OpenIddict model extension as the runtime registration
/// (<see cref="IdentityDbModel.ApplyOpenIddict"/>); otherwise the generated migration would omit the
/// OpenIddict tables. No connection string is needed for design-time model building.
/// </summary>
public class MySqlIdentityDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseMySql(
            ServerVersion.Create(10, 11, 0, ServerType.MariaDb),
            b => b.MigrationsAssembly("Eryph.IdentityDb.MySql"));
        IdentityDbModel.ApplyOpenIddict(optionsBuilder);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
