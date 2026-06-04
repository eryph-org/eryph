using Microsoft.EntityFrameworkCore;
using Microting.EntityFrameworkCore.MySql.Infrastructure;

namespace Eryph.IdentityDb.MySql;

/// <summary>
/// Configures the <see cref="IdentityDbContext"/> for MariaDB (the standalone identity host's own
/// database). The migrations are discovered from <see cref="MySqlIdentityDbContext"/>'s own assembly
/// (this one), so no explicit MigrationsAssembly is needed — mirroring <c>MySqlStateStoreContextConfigurer</c>.
/// </summary>
public class MySqlIdentityDbContextConfigurer(string connectionString)
    : IDbContextConfigurer<IdentityDbContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseMySql(
            connectionString,
            ServerVersion.Create(10, 11, 0, ServerType.MariaDb));
    }
}
