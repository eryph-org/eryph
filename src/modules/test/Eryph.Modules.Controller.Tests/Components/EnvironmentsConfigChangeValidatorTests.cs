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
public class MySqlEnvironmentsConfigChangeValidatorTests(
    ITestOutputHelper outputHelper, MySqlFixture databaseFixture)
    : EnvironmentsConfigChangeValidatorTests(outputHelper, databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteEnvironmentsConfigChangeValidatorTests(
    ITestOutputHelper outputHelper, SqliteFixture databaseFixture)
    : EnvironmentsConfigChangeValidatorTests(outputHelper, databaseFixture);

/// <summary>
/// Verifies that an environment cannot be moved to another site or removed while it still has
/// resources. Their site is pinned and would not move with it, so the environment would stop being a
/// single locality.
/// </summary>
public abstract class EnvironmentsConfigChangeValidatorTests(
    ITestOutputHelper outputHelper, IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    private const string StagingInBerlin =
        """
        environments:
        - name: staging
          site: berlin
        """;

    private const string StagingInMunich =
        """
        environments:
        - name: staging
          site: munich
        """;

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Fact]
    public async Task Unused_environment_can_be_moved_to_another_site()
    {
        await Author(StagingInBerlin);

        var errors = await Validate(StagingInMunich);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Environment_with_a_catlet_cannot_be_moved_to_another_site()
    {
        await Author(StagingInBerlin);
        await AddCatlet("staging");

        var errors = await Validate(StagingInMunich);

        errors.Should().ContainSingle()
            .Which.Should().Contain("cannot be moved").And.Contain("still has resources");
    }

    [Fact]
    public async Task Environment_with_a_catlet_cannot_be_removed()
    {
        await Author(StagingInBerlin);
        await AddCatlet("staging");

        var errors = await Validate("environments: []");

        errors.Should().ContainSingle().Which.Should().Contain("cannot be removed");
    }

    [Fact]
    public async Task Environment_with_a_catlet_can_be_re_authored_unchanged()
    {
        await Author(StagingInBerlin);
        await AddCatlet("staging");

        var errors = await Validate(StagingInBerlin);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_catlet_in_another_environment_does_not_block_the_change()
    {
        await Author(StagingInBerlin);
        await AddCatlet("default");

        var errors = await Validate(StagingInMunich);

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Nothing_is_refused_when_no_configuration_was_authored_yet()
    {
        var errors = await Validate(StagingInMunich);

        errors.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<string>> Validate(string payload)
    {
        await using var scope = CreateScope();
        var validator = new EnvironmentsConfigChangeValidator(
            Store(scope), scope.GetInstance<IStateStore>());

        return await validator.ValidateChanges(payload, default);
    }

    private async Task Author(string payload)
    {
        await using var scope = CreateScope();
        await Store(scope).AddVersionAsync(
            ConfigDomain.Environments, ConfigScope.Default, payload, "test", default);
        await scope.GetInstance<IStateStore>().SaveChangesAsync();
    }

    private async Task AddCatlet(string environment)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = Guid.NewGuid(),
            ProjectId = EryphConstants.DefaultProjectId,
            SiteId = EryphConstants.DefaultSiteId,
            Name = "test-catlet",
            Environment = environment,
            DataStore = EryphConstants.DefaultDataStoreName,
        });
        await stateStore.SaveChangesAsync();
    }

    private static AuthoredConfigStore Store(Scope dbScope) =>
        new(dbScope.GetInstance<IStateStoreRepository<AuthoredConfig>>(),
            new Mock<IDistributedLockScopeHolder>().Object);
}
