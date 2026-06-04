using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.IdentityDb.Sqlite;

public static class ContainerExtensions
{
    /// <summary>
    /// Registers the SQLite identity store (eryph-zero) on the <see cref="SqliteIdentityDbContext"/>, using
    /// the given connection string.
    /// </summary>
    public static SimpleInjectorAddOptions RegisterSqliteIdentityStore(
        this SimpleInjectorAddOptions options,
        string connectionString) =>
        options.RegisterIdentityStore<SqliteIdentityDbContext>(
            new SqliteIdentityDbContextConfigurer(connectionString));
}
