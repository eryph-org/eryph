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

    private async Task<LanguageExt.Either<LanguageExt.Common.Error, Guid>> Resolve(string environment)
    {
        await using var scope = CreateScope();
        var resolver = new SiteResolver(
            Store(scope),
            scope.GetInstance<IStateStoreRepository<Site>>());

        return await resolver.ResolveSite(EnvironmentName.New(environment));
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
