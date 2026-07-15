using Eryph.Core;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Components;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlSitesConfigRealizerTests(
    ITestOutputHelper outputHelper, MySqlFixture databaseFixture)
    : SitesConfigRealizerTests(outputHelper, databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteSitesConfigRealizerTests(
    ITestOutputHelper outputHelper, SqliteFixture databaseFixture)
    : SitesConfigRealizerTests(outputHelper, databaseFixture);

/// <summary>
/// The sites an operator declares are materialized as records, so an environment can only reference
/// a site that exists and resources can be pinned to it.
/// </summary>
public abstract class SitesConfigRealizerTests(
    ITestOutputHelper outputHelper, IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Fact]
    public async Task Declared_sites_are_created()
    {
        await Realize("berlin", "munich");

        await WithScope(async stateStore =>
        {
            var sites = await stateStore.For<Site>().ListAsync();

            sites.Select(s => s.Name).Should()
                .BeEquivalentTo([EryphConstants.DefaultSiteName, "berlin", "munich"]);
        });
    }

    [Fact]
    public async Task Realizing_the_same_sites_again_does_not_duplicate_them()
    {
        await Realize("berlin");
        await Realize("berlin");

        await WithScope(async stateStore =>
        {
            var sites = await stateStore.For<Site>().ListAsync();

            sites.Should().HaveCount(2); // the default site and berlin
        });
    }

    [Fact]
    public async Task A_site_which_is_no_longer_declared_is_removed()
    {
        await Realize("berlin", "munich");

        await Realize("berlin");

        await WithScope(async stateStore =>
        {
            var sites = await stateStore.For<Site>().ListAsync();

            sites.Select(s => s.Name).Should()
                .BeEquivalentTo([EryphConstants.DefaultSiteName, "berlin"]);
        });
    }

    [Fact]
    public async Task The_default_site_is_never_removed()
    {
        // It is reserved: it is seeded, never declared, and every resource falls back to it.
        await Realize("berlin");

        await WithScope(async stateStore =>
        {
            var site = await stateStore.For<Site>().GetByIdAsync(EryphConstants.DefaultSiteId);

            site.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task A_site_with_resources_cannot_be_removed()
    {
        await Realize("berlin");
        var berlinId = await SiteId("berlin");
        await AddCatletInSite(berlinId);

        var act = () => Realize();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*'berlin' cannot be removed because it still has resources*");
    }

    private async Task Realize(params string[] siteNames)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        var realizer = new SitesConfigRealizer(stateStore);

        await realizer.RealizeSites(
            new EnvironmentsConfig
            {
                Sites = siteNames.Select(n => new SiteConfig { Name = n }).ToArray(),
            },
            default);

        await stateStore.SaveChangesAsync();
    }

    private async Task<Guid> SiteId(string name)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        var sites = await stateStore.For<Site>().ListAsync();

        return sites.First(s => s.Name == name).Id;
    }

    private async Task AddCatletInSite(Guid siteId)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = Guid.NewGuid(),
            ProjectId = EryphConstants.DefaultProjectId,
            SiteId = siteId,
            Name = "test-catlet",
            Environment = "staging",
            DataStore = EryphConstants.DefaultDataStoreName,
        });
        await stateStore.SaveChangesAsync();
    }

    private async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        await func(scope.GetInstance<IStateStore>());
    }
}
