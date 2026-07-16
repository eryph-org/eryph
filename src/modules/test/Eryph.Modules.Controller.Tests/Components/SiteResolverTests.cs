using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Moq;
using SimpleInjector;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Components;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlSiteResolverTests(ITestOutputHelper outputHelper, MySqlFixture databaseFixture)
    : SiteResolverTests(outputHelper, databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteSiteResolverTests(ITestOutputHelper outputHelper, SqliteFixture databaseFixture)
    : SiteResolverTests(outputHelper, databaseFixture);

/// <summary>
/// Verifies that an environment resolves to the site which realizes it, from the authored
/// environment catalog. This is only consulted when a resource is created; the site of an existing
/// resource is pinned on the resource itself.
/// </summary>
public abstract class SiteResolverTests(ITestOutputHelper outputHelper, IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    private static readonly Guid BerlinSiteId = new("6f0b7c1a-2d3e-4f5a-8b9c-0d1e2f3a4b5c");

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await stateStore.For<Site>().AddAsync(new Site
        {
            Id = BerlinSiteId,
            Name = "berlin",
        });
    }

    [Fact]
    public async Task Default_environment_resolves_to_the_default_site_without_configuration()
    {
        var result = await Resolve("default");

        result.IfLeft(e => throw new Exception(e.Message)).Should().Be(EryphConstants.DefaultSiteId);
    }

    [Fact]
    public async Task Configured_environment_resolves_to_its_site()
    {
        await AuthorEnvironments(
            """
            environments:
            - name: staging
              site: berlin
            """);

        var result = await Resolve("staging");

        result.IfLeft(e => throw new Exception(e.Message)).Should().Be(BerlinSiteId);
    }

    [Fact]
    public async Task Unknown_environment_is_an_error()
    {
        await AuthorEnvironments(
            """
            environments:
            - name: staging
              site: berlin
            """);

        var result = await Resolve("prod");

        result.IsLeft.Should().BeTrue();
        result.IfRight(_ => throw new Exception("expected an error"));
        result.MapLeft(e => e.Message.Should().Contain("'prod' is not part of the environment configuration"));
    }

    [Fact]
    public async Task Environment_configured_for_a_missing_site_is_an_error()
    {
        await AuthorEnvironments(
            """
            environments:
            - name: staging
              site: munich
            """);

        var result = await Resolve("staging");

        result.IsLeft.Should().BeTrue();
        result.MapLeft(e => e.Message.Should().Contain("site 'munich'"));
    }

    [Fact]
    public async Task Environment_is_unknown_when_nothing_was_authored()
    {
        var result = await Resolve("staging");

        result.IsLeft.Should().BeTrue();
    }

    [Fact]
    public async Task Environment_declared_only_by_the_host_defaults_resolves()
    {
        // eryph-zero declares its environments in agentsettings.yml and authors nothing. The agents
        // are handed that catalog, so the controller must resolve from it too — otherwise every
        // deployment into an environment which worked before the catalog existed is refused.
        var result = await Resolve(
            "staging",
            defaults: new EnvironmentsConfig
            {
                Environments =
                [
                    new EnvironmentConfig
                    {
                        Name = "staging", Site = EryphConstants.DefaultSiteName,
                    },
                ],
            });

        result.IsRight.Should().BeTrue();
        result.IfRight(siteId => siteId.Should().Be(EryphConstants.DefaultSiteId));
    }

    [Fact]
    public async Task Authored_configuration_wins_over_the_host_defaults()
    {
        await AuthorEnvironments(
            """
            sites:
            - name: munich
            environments:
            - name: staging
              site: munich
            """);

        var result = await Resolve(
            "staging",
            defaults: new EnvironmentsConfig
            {
                Environments =
                [
                    new EnvironmentConfig
                    {
                        Name = "staging", Site = EryphConstants.DefaultSiteName,
                    },
                ],
            });

        // The site does not exist (nothing realized it in this test), which proves the authored
        // value was read rather than the default one.
        result.IsLeft.Should().BeTrue();
        result.MapLeft(e => e.Message.Should().Contain("site 'munich'"));
    }

    private async Task<LanguageExt.Either<LanguageExt.Common.Error, Guid>> Resolve(
        string environment, EnvironmentsConfig? defaults = null)
    {
        await using var scope = CreateScope();
        var resolver = new SiteResolver(
            new CurrentEnvironmentsConfig(
                Store(scope), new StubEnvironmentsDefaults(defaults ?? new EnvironmentsConfig())),
            scope.GetInstance<IStateStoreRepository<Site>>());

        return await resolver.ResolveSite(EnvironmentName.New(environment));
    }

    /// <summary>Stands in for the host-wired defaults; the split runtime's is an empty catalog.</summary>
    private sealed class StubEnvironmentsDefaults(EnvironmentsConfig config)
        : IEnvironmentsConfigDefaultsProvider
    {
        public LanguageExt.EitherAsync<LanguageExt.Common.Error, EnvironmentsConfig>
            GetDefaultEnvironmentsConfig() =>
            LanguageExt.Prelude.RightAsync<LanguageExt.Common.Error, EnvironmentsConfig>(config);
    }

    private async Task AuthorEnvironments(string payload)
    {
        await using var scope = CreateScope();
        await Store(scope).AddVersionAsync(
            ConfigDomain.Environments, ConfigScope.Default, payload, "test", default);
        await scope.GetInstance<IStateStore>().SaveChangesAsync();
    }

    private static AuthoredConfigStore Store(Scope dbScope) =>
        new(dbScope.GetInstance<IStateStoreRepository<AuthoredConfig>>(),
            new Mock<IDistributedLockScopeHolder>().Object);
}
