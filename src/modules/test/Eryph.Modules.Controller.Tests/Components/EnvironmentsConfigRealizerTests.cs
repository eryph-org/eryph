using Eryph.Core;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Components;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlEnvironmentsConfigRealizerTests(
    ITestOutputHelper outputHelper, MySqlFixture databaseFixture)
    : EnvironmentsConfigRealizerTests(outputHelper, databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteEnvironmentsConfigRealizerTests(
    ITestOutputHelper outputHelper, SqliteFixture databaseFixture)
    : EnvironmentsConfigRealizerTests(outputHelper, databaseFixture);

/// <summary>
/// The sites an operator declares are materialized as records, so an environment can only reference
/// a site that exists and resources can be pinned to it.
/// </summary>
public abstract class EnvironmentsConfigRealizerTests(
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

    [Fact]
    public async Task Environment_bindings_are_realized_and_resolve()
    {
        // The bindings are the point: resolving a site reads these records, never the configuration.
        await RealizeConfig(
            """
            sites:
            - name: berlin
            environments:
            - name: staging
              site: berlin
            """);

        await WithScope(async stateStore =>
        {
            var berlin = await stateStore.For<Site>().GetBySpecAsync(new SiteSpecs.GetByName("berlin"));
            var staging = await stateStore.For<Eryph.StateDb.Model.Environment>()
                .GetBySpecAsync(new EnvironmentSpecs.GetByName("staging"));

            staging.Should().NotBeNull();
            staging!.SiteId.Should().Be(berlin!.Id);
        });
    }

    [Fact]
    public async Task An_environment_without_a_site_is_realized_by_the_default_site()
    {
        // The omitted site autofills to the default one, the same as when it is authored.
        await RealizeConfig(
            """
            environments:
            - name: staging
            """);

        await WithScope(async stateStore =>
        {
            var staging = await stateStore.For<Eryph.StateDb.Model.Environment>()
                .GetBySpecAsync(new EnvironmentSpecs.GetByName("staging"));

            staging!.SiteId.Should().Be(EryphConstants.DefaultSiteId);
        });
    }

    [Fact]
    public async Task A_dropped_environment_is_removed_and_re_realizing_is_idempotent()
    {
        const string config =
            """
            sites:
            - name: berlin
            environments:
            - name: staging
              site: berlin
            """;
        await RealizeConfig(config);
        // Every refresh realizes again, so applying the same catalog twice must not duplicate it.
        await RealizeConfig(config);

        await WithScope(async stateStore =>
        {
            (await stateStore.For<Eryph.StateDb.Model.Environment>().ListAsync())
                .Should().ContainSingle();
        });

        await RealizeConfig("environments: []");

        await WithScope(async stateStore =>
        {
            (await stateStore.For<Eryph.StateDb.Model.Environment>().ListAsync()).Should().BeEmpty();
        });
    }

    private async Task RealizeConfig(string payload)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await new EnvironmentsConfigRealizer(stateStore).RealizeEnvironments(
            EnvironmentsConfigYamlSerializer.Deserialize(payload), default);
        await stateStore.SaveChangesAsync();
    }

    private async Task Realize(params string[] siteNames)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        var realizer = new EnvironmentsConfigRealizer(stateStore);

        await realizer.RealizeEnvironments(
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
