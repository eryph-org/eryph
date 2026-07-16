using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;
using Eryph.Modules.Controller.Seeding;
using Eryph.Modules.Controller.Tests.ChangeTracking;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Seeding;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlAuthoredConfigSeederTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : AuthoredConfigSeederTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteAuthoredConfigSeederTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : AuthoredConfigSeederTests(databaseFixture, outputHelper);

/// <summary>
/// The operator-authored configuration lives only in the state database, which eryph-zero re-creates
/// whenever the schema changes. Without a mirror every authored value would be lost on an update —
/// including the environments and the sites they are realized by, which resources are pinned to, and
/// without which a project's networks cannot even be seeded.
/// </summary>
public abstract class AuthoredConfigSeederTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SeederTestBase(databaseFixture, outputHelper)
{
    private const string Payload =
        """
        sites:
        - name: berlin
        environments:
        - name: staging
          site: berlin
        """;

    [Fact]
    public async Task Authored_config_is_restored_from_the_mirror_with_its_sites()
    {
        await WriteMirror();

        await ExecuteSeeder();

        await WithScope(async stateStore =>
        {
            var authored = await stateStore.For<AuthoredConfig>().ListAsync();
            authored.Should().ContainSingle()
                .Which.Domain.Should().Be(ConfigDomain.Environments);

            // The sites are records, so they were dropped with the database. They must come back
            // too, or nothing could be pinned to them.
            var sites = await stateStore.For<Site>().ListAsync();
            sites.Select(s => s.Name).Should()
                .BeEquivalentTo([EryphConstants.DefaultSiteName, "berlin"]);
        });
    }

    [Fact]
    public async Task An_existing_authored_config_is_not_overwritten_by_the_mirror()
    {
        await WriteMirror();
        await WithScope(async stateStore =>
        {
            await stateStore.For<AuthoredConfig>().AddAsync(new AuthoredConfig
            {
                Id = Guid.NewGuid(),
                Domain = ConfigDomain.Environments,
                Scope = ConfigScope.Default,
                Version = 7,
                Payload = "environments: []",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await stateStore.SaveChangesAsync();
        });

        await ExecuteSeeder();

        await WithScope(async stateStore =>
        {
            var authored = await stateStore.For<AuthoredConfig>().ListAsync();

            authored.Should().ContainSingle().Which.Version.Should().Be(7);
        });
    }

    [Fact]
    public async Task Nothing_is_restored_when_there_is_no_mirror()
    {
        await ExecuteSeeder();

        await WithScope(async stateStore =>
        {
            var authored = await stateStore.For<AuthoredConfig>().ListAsync();

            authored.Should().BeEmpty();
        });
    }

    // Serialized exactly as the change handler writes it, so the test pins the real round trip
    // rather than a hand-written shape which could drift from it.
    private async Task WriteMirror() =>
        await MockFileSystem.File.WriteAllTextAsync(
            Path.Combine(ChangeTrackingConfig.AuthoredConfigsPath, "authored.json"),
            System.Text.Json.JsonSerializer.Serialize(new AuthoredConfigsConfigModel
            {
                AuthoredConfigs =
                [
                    new AuthoredConfigConfigModel
                    {
                        Domain = nameof(ConfigDomain.Environments),
                        Scope = ConfigScope.Default,
                        Version = 3,
                        Payload = Payload,
                        CreatedBy = "alice",
                    },
                ],
            }));

    private async Task ExecuteSeeder()
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        var seeder = new AuthoredConfigSeeder(
            ChangeTrackingConfig,
            MockFileSystem,
            new SitesConfigRealizer(stateStore),
            stateStore,
            NullLogger.Instance);

        await seeder.Execute(default);
    }

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        await func(scope.GetInstance<IStateStore>());
    }
}
