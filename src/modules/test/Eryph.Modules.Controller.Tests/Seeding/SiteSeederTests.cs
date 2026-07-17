using Eryph.Core;
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
public class MySqlSiteSeederTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SiteSeederTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteSiteSeederTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SiteSeederTests(databaseFixture, outputHelper);

public abstract class SiteSeederTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SeederTestBase(databaseFixture, outputHelper)
{
    // The seeder itself is under test, so the test base must not create the site for us.
    protected override bool SeedDefaultSite => false;

    [Fact]
    public async Task Default_site_is_seeded()
    {
        await ExecuteSeeder();

        await WithScope(async stateStore =>
        {
            var sites = await stateStore.For<Site>().ListAsync();

            var site = Assert.Single(sites);
            Assert.Equal(EryphConstants.DefaultSiteId, site.Id);
            Assert.Equal(EryphConstants.DefaultSiteName, site.Name);
        });
    }

    [Fact]
    public async Task Default_site_is_not_seeded_twice()
    {
        // The seeder itself is executed twice rather than the whole seeding host: re-running the
        // host would also re-run the other seeders over the config files the first run wrote, which
        // is a different thing to test.
        await WithScope(async stateStore =>
        {
            await new SiteSeeder(NullLogger.Instance, stateStore).Execute(default);
            await new SiteSeeder(NullLogger.Instance, stateStore).Execute(default);
        });

        await WithScope(async stateStore =>
        {
            var sites = await stateStore.For<Site>().ListAsync();

            Assert.Single(sites);
        });
    }

    private async Task ExecuteSeeder()
    {
        using var host = CreateHost();
        await host.StartAsync();
        await ChangeTrackingTestHelpers.WaitForIdleAsync(host, TimeSpan.FromSeconds(10));
        await host.StopAsync();
    }

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(stateStore);
    }
}
