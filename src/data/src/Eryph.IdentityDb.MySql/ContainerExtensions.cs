using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.IdentityDb.MySql;

public static class ContainerExtensions
{
    /// <summary>
    /// Registers the MariaDB identity store (the standalone identity host) on the
    /// <see cref="MySqlIdentityDbContext"/>, using the given connection string.
    /// </summary>
    public static SimpleInjectorAddOptions RegisterMySqlIdentityStore(
        this SimpleInjectorAddOptions options,
        string connectionString) =>
        options.RegisterIdentityStore<MySqlIdentityDbContext>(
            new MySqlIdentityDbContextConfigurer(connectionString));
}
