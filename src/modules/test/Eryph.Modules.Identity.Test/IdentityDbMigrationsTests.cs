using Eryph.IdentityDb;
using Eryph.IdentityDb.MySql;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Eryph.Modules.Identity.Test;

public class IdentityDbMigrationsTests
{
    [Fact]
    public void MySql_configured_context_discovers_the_initial_migration()
    {
        // Guards the linchpin of the persistence feature: the runtime context (IdentityDbContext, in
        // assembly Eryph.IdentityDb) must find the migrations that live in the Eryph.IdentityDb.MySql
        // assembly via the configurer's MigrationsAssembly. A typo in that string, or a migration that
        // is not compiled into the output assembly, would make MigrateAsync a silent no-op and leave an
        // empty schema. GetMigrations() reads the migrations assembly without opening a connection, so a
        // throwaway connection string is sufficient.
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        new MySqlIdentityDbContextConfigurer(
                "Server=localhost;Database=eryph_identity;User Id=eryph;Password=ignored")
            .Configure(options);
        IdentityDbModel.ApplyOpenIddict(options);

        using var context = new IdentityDbContext(options.Options);

        context.Database.GetMigrations().Should().Contain(
            m => m.EndsWith("InitialCreate"),
            "the MariaDB migrations assembly must be discoverable from the runtime context");
    }
}
