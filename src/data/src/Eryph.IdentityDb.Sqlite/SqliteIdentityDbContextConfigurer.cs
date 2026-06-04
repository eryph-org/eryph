using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb.Sqlite;

/// <summary>
/// Configures the <see cref="IdentityDbContext"/> for SQLite — the eryph-zero identity store, a
/// disposable on-disk database that is mirrored to config files and rebuilt from them. The migrations are
/// discovered from <see cref="SqliteIdentityDbContext"/>'s own assembly (this one), so no explicit
/// MigrationsAssembly is needed — mirroring <c>SqliteStateStoreContextConfigurer</c>.
/// </summary>
public class SqliteIdentityDbContextConfigurer(string connectionString)
    : IDbContextConfigurer<IdentityDbContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseSqlite(connectionString);
    }
}
