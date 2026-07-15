using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Eryph.StateDb.Tests;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlSiteTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SiteTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteSiteTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SiteTests(databaseFixture, outputHelper);

public abstract class SiteTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    /// <summary>
    /// A specification is project level and deploys into many environments, hence into
    /// many sites. It must therefore not be site bound, which this test pins down: the
    /// resource base class must not gain a site.
    /// </summary>
    [Fact]
    public void CatletSpecification_is_not_site_bound()
    {
        Assert.False(typeof(ISiteBound).IsAssignableFrom(typeof(CatletSpecification)));

        Assert.True(typeof(ISiteBound).IsAssignableFrom(typeof(Catlet)));
        Assert.True(typeof(ISiteBound).IsAssignableFrom(typeof(CatletFarm)));
        Assert.True(typeof(ISiteBound).IsAssignableFrom(typeof(VirtualDisk)));
        Assert.True(typeof(ISiteBound).IsAssignableFrom(typeof(VirtualNetwork)));
    }

    [Fact]
    public async Task Site_name_must_be_unique()
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        await stateStore.For<Site>().AddAsync(new Site
        {
            Id = Guid.NewGuid(),
            Name = EryphConstants.DefaultSiteName,
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => stateStore.SaveChangesAsync());
    }

    /// <summary>
    /// The site of a resource is pinned. Removing a site which still has resources would
    /// leave them without a location, so the delete must be refused instead of cascading.
    /// </summary>
    [Fact]
    public async Task Site_with_resources_cannot_be_deleted()
    {
        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            await stateStore.For<Catlet>().AddAsync(new Catlet
            {
                Id = Guid.NewGuid(),
                ProjectId = EryphConstants.DefaultProjectId,
                SiteId = EryphConstants.DefaultSiteId,
                Name = "test-catlet",
                Environment = EryphConstants.DefaultEnvironmentName,
                DataStore = EryphConstants.DefaultDataStoreName,
            });
            await stateStore.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            var site = await stateStore.For<Site>().GetByIdAsync(EryphConstants.DefaultSiteId);
            Assert.NotNull(site);

            await stateStore.For<Site>().DeleteAsync(site);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => stateStore.SaveChangesAsync());
        }
    }
}
