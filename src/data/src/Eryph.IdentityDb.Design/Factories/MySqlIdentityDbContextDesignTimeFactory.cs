using Eryph.IdentityDb.MySql;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microting.EntityFrameworkCore.MySql.Infrastructure;

namespace Eryph.IdentityDb.Design.Factories;

/// <summary>
/// Design-time factory for the standalone identity host's MariaDB store. The OpenIddict model extension
/// applied at runtime (<see cref="IdentityDbModel.ApplyOpenIddict"/>) must be applied here too, or a
/// generated migration would omit every OpenIddict table. Migrations live in the
/// <c>Eryph.IdentityDb.MySql</c> assembly (this context's own assembly).
/// </summary>
/// <remarks>
/// Generate with:
/// <code>
/// dotnet ef migrations add InitialCreate -p src/data/src/Eryph.IdentityDb.MySql -s src/data/src/Eryph.IdentityDb.Design -c MySqlIdentityDbContext
/// </code>
/// </remarks>
[UsedImplicitly]
public class MySqlIdentityDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<MySqlIdentityDbContext>
{
    public MySqlIdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MySqlIdentityDbContext>();
        optionsBuilder.UseMySql(ServerVersion.Create(10, 11, 0, ServerType.MariaDb));
        IdentityDbModel.ApplyOpenIddict(optionsBuilder);

        return new MySqlIdentityDbContext(optionsBuilder.Options);
    }
}
