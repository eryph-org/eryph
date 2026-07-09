using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Microsoft.EntityFrameworkCore;
using Moq;
using SimpleInjector;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Components;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlAuthoredConfigStoreTests(ITestOutputHelper outputHelper, MySqlFixture databaseFixture)
    : AuthoredConfigStoreTests(outputHelper, databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteAuthoredConfigStoreTests(ITestOutputHelper outputHelper, SqliteFixture databaseFixture)
    : AuthoredConfigStoreTests(outputHelper, databaseFixture);

/// <summary>
/// Verifies the versioned authored-config store against a real database: versions are monotonic per
/// (domain, scope), the current value is the highest version, history is complete and newest-first,
/// and scopes version independently.
/// </summary>
public abstract class AuthoredConfigStoreTests(ITestOutputHelper outputHelper, IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    // Each authoring is its own unit of work in production (one bus message); mirror that by opening a
    // fresh scope per operation so the store sees committed state, not uncommitted inserts.
    private async Task<AuthoredConfig> AddVersion(
        ConfigDomain domain, string scope, string payload, string? author = null)
    {
        await using var dbScope = CreateScope();
        var entry = await Store(dbScope).AddVersionAsync(domain, scope, payload, author, default);
        await dbScope.GetInstance<IStateStore>().SaveChangesAsync();
        return entry;
    }

    private async Task<AuthoredConfig?> GetCurrent(ConfigDomain domain, string scope)
    {
        await using var dbScope = CreateScope();
        return await Store(dbScope).GetCurrentAsync(domain, scope, default);
    }

    private async Task<IReadOnlyList<AuthoredConfig>> GetHistory(ConfigDomain domain, string scope)
    {
        await using var dbScope = CreateScope();
        return await Store(dbScope).GetHistoryAsync(domain, scope, default);
    }

    private static AuthoredConfigStore Store(Scope dbScope) =>
        new(dbScope.GetInstance<IStateStoreRepository<AuthoredConfig>>(),
            new Mock<IDistributedLockScopeHolder>().Object);

    [Fact]
    public async Task AddVersion_appends_monotonic_versions_and_GetCurrent_returns_the_highest()
    {
        var v1 = await AddVersion(ConfigDomain.StorageConfig, ConfigScope.Default, "p1", "alice");
        var v2 = await AddVersion(ConfigDomain.StorageConfig, ConfigScope.Default, "p2", "bob");

        v1.Version.Should().Be(1);
        v2.Version.Should().Be(2);

        var current = await GetCurrent(ConfigDomain.StorageConfig, ConfigScope.Default);
        current.Should().NotBeNull();
        current!.Version.Should().Be(2);
        current.Payload.Should().Be("p2");
        current.CreatedBy.Should().Be("bob");
    }

    [Fact]
    public async Task GetHistory_returns_all_versions_newest_first()
    {
        await AddVersion(ConfigDomain.StorageConfig, ConfigScope.Default, "p1");
        await AddVersion(ConfigDomain.StorageConfig, ConfigScope.Default, "p2");
        await AddVersion(ConfigDomain.StorageConfig, ConfigScope.Default, "p3");

        var history = await GetHistory(ConfigDomain.StorageConfig, ConfigScope.Default);

        history.Select(h => h.Version).Should().Equal(3, 2, 1);
        history.Select(h => h.Payload).Should().Equal("p3", "p2", "p1");
    }

    [Fact]
    public async Task Duplicate_domain_scope_version_is_rejected_by_the_unique_index()
    {
        // The unique (Domain, Scope, Version) index is the real cross-controller guard: two controllers
        // that both allocate the same next version collide here, and the losing unit of work retries and
        // re-versions — so the write converges rather than being lost.
        await using var dbScope = CreateScope();
        var repository = dbScope.GetInstance<IStateStoreRepository<AuthoredConfig>>();

        await repository.AddAsync(Entry(1, "a"));
        await repository.AddAsync(Entry(1, "b"));

        var save = () => dbScope.GetInstance<IStateStore>().SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();

        static AuthoredConfig Entry(long version, string payload) => new()
        {
            Id = Guid.NewGuid(),
            Domain = ConfigDomain.StorageConfig,
            Scope = ConfigScope.Default,
            Version = version,
            Payload = payload,
        };
    }

    [Fact]
    public async Task Versions_are_independent_per_scope()
    {
        var prodScope = ConfigScope.ForEnvironment("prod");
        await AddVersion(ConfigDomain.StorageConfig, ConfigScope.Default, "default-1");
        await AddVersion(ConfigDomain.StorageConfig, prodScope, "prod-1");
        await AddVersion(ConfigDomain.StorageConfig, prodScope, "prod-2");

        (await GetCurrent(ConfigDomain.StorageConfig, ConfigScope.Default))!.Version.Should().Be(1);

        var prod = await GetCurrent(ConfigDomain.StorageConfig, prodScope);
        prod!.Version.Should().Be(2);
        prod.Payload.Should().Be("prod-2");
    }

    [Fact]
    public async Task GetCurrent_is_null_when_nothing_is_authored()
    {
        (await GetCurrent(ConfigDomain.Endpoints, ConfigScope.Default)).Should().BeNull();
    }
}
