using Eryph.Core;
using Eryph.Modules.Controller.Tests.ChangeTracking;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
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
        await ExecuteSeeder();
        await ExecuteSeeder();

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
