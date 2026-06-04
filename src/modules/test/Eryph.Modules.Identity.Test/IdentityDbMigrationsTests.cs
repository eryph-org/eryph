using Eryph.IdentityDb;
using Eryph.IdentityDb.MySql;
using Eryph.IdentityDb.Sqlite;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Eryph.Modules.Identity.Test;

public class IdentityDbMigrationsTests
{
    [Fact]
    public void MySql_configured_context_discovers_the_initial_migration()
    {
        // Guards the linchpin of the persistence feature: the runtime context (MySqlIdentityDbContext)
        // must find the migrations that live in its own assembly (Eryph.IdentityDb.MySql). A migration
        // that is not compiled into the output assembly, or whose [DbContext] attribute does not match the
        // derived context, would make MigrateAsync a silent no-op and leave an empty schema. GetMigrations()
        // reads the migrations assembly without opening a connection, so a throwaway connection string is
        // sufficient.
        var options = new DbContextOptionsBuilder<MySqlIdentityDbContext>();
        new MySqlIdentityDbContextConfigurer(
                "Server=localhost;Database=eryph_identity;User Id=eryph;Password=ignored")
            .Configure(options);
        IdentityDbModel.ApplyOpenIddict(options);

        using var context = new MySqlIdentityDbContext(options.Options);

        context.Database.GetMigrations().Should().Contain(
            m => m.EndsWith("InitialCreate"),
            "the MariaDB migrations must be discoverable from the derived context's assembly");
    }

    [Fact]
    public void Sqlite_configured_context_discovers_the_initial_migration()
    {
        // Same guard for the eryph-zero SQLite store: migrations that did not compile into
        // Eryph.IdentityDb.Sqlite, or a [DbContext] attribute mismatch, would make MigrateIdentityDbHandler
        // a silent no-op and leave eryph-zero with an empty identity DB.
        var options = new DbContextOptionsBuilder<SqliteIdentityDbContext>();
        new SqliteIdentityDbContextConfigurer("Data Source=:memory:").Configure(options);
        IdentityDbModel.ApplyOpenIddict(options);

        using var context = new SqliteIdentityDbContext(options.Options);

        context.Database.GetMigrations().Should().Contain(
            m => m.EndsWith("InitialCreate"),
            "the SQLite migrations must be discoverable from the derived context's assembly");
    }
}
