using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb.Sqlite;

/// <summary>
/// Configures the <see cref="IdentityDbContext"/> for SQLite — the eryph-zero identity store, a
/// disposable on-disk database that is mirrored to config files and rebuilt from them. The migrations
/// live in this assembly; the context's <c>ConfigureConventions</c> applies the DateTimeOffset
/// conversion when this provider is active.
/// </summary>
public class SqliteIdentityDbContextConfigurer(string connectionString)
    : IDbContextConfigurer<IdentityDbContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseSqlite(
            connectionString,
            b => b.MigrationsAssembly("Eryph.IdentityDb.Sqlite"));
    }
}
