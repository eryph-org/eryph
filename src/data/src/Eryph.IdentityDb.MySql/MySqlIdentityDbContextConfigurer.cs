using Microsoft.EntityFrameworkCore;
using Microting.EntityFrameworkCore.MySql.Infrastructure;

namespace Eryph.IdentityDb.MySql;

/// <summary>
/// Configures the <see cref="IdentityDbContext"/> for MariaDB (the standalone identity host's own
/// database). The migrations live in this assembly rather than the context's assembly, so the context
/// type stays shared across packagings while the provider-specific migrations are isolated here.
/// </summary>
public class MySqlIdentityDbContextConfigurer(string connectionString)
    : IDbContextConfigurer<IdentityDbContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseMySql(
            connectionString,
            ServerVersion.Create(10, 11, 0, ServerType.MariaDb),
            b => b.MigrationsAssembly("Eryph.IdentityDb.MySql"));
    }
}
