using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.StateDb.Tests;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlVirtualDiskSpecsTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : VirtualDiskSpecsTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteVirtualDiskSpecsTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : VirtualDiskSpecsTests(databaseFixture, outputHelper);

public abstract class VirtualDiskSpecsTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : StateDbTestBase(databaseFixture, outputHelper)
{
    private const string AgentName = "testhost";

    // A gene-pool disk: it carries no storage identifier and is therefore
    // persisted with StorageIdentifier == null.
    private static readonly Guid GeneDiskId = new("3f6c1d2e-9a4b-4c8d-bf11-7e2a5d0c4b91");
    private static readonly Guid GeneDiskIdentifier = new("c1a2b3c4-d5e6-47a8-9b0c-1d2e3f4a5b6c");

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = GeneDiskId,
            Name = "sda",
            DiskIdentifier = GeneDiskIdentifier,
            StorageIdentifier = null,
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            GeneSet = "dbosoft/winsrv2025-standard/20260611",
            GeneName = "sda",
            GeneArchitecture = "hyperv/amd64",
        });
    }

    [Fact]
    public async Task GetByLocation_DiskHasNoStorageIdentifier_MatchesExistingDisk()
    {
        // Regression test for the gene-pool disk duplication bug: a disk that was
        // persisted with StorageIdentifier == null must be found again during
        // inventory. Passing the nullable value through lets EF emit "IS NULL" so
        // the lookup matches; otherwise every inventory run inserts a duplicate.
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var result = await stateStore.For<VirtualDisk>().ListAsync(
            new VirtualDiskSpecs.GetByLocation(
                EryphConstants.DefaultProjectId,
                EryphConstants.DefaultDataStoreName,
                EryphConstants.DefaultEnvironmentName,
                storageIdentifier: null,
                "sda",
                GeneDiskIdentifier));

        result.Should().ContainSingle()
            .Which.Id.Should().Be(GeneDiskId);
    }

    [Fact]
    public async Task GetByLocation_DiskHasNoStorageIdentifier_EmptyStringDoesNotMatch()
    {
        // Documents the original bug: querying a null-StorageIdentifier disk with an
        // empty string (the previous "?? \"\"" behaviour) fails to match, which is what
        // caused the duplicate inserts.
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var result = await stateStore.For<VirtualDisk>().ListAsync(
            new VirtualDiskSpecs.GetByLocation(
                EryphConstants.DefaultProjectId,
                EryphConstants.DefaultDataStoreName,
                EryphConstants.DefaultEnvironmentName,
                storageIdentifier: "",
                "sda",
                GeneDiskIdentifier));

        result.Should().BeEmpty();
    }
}
