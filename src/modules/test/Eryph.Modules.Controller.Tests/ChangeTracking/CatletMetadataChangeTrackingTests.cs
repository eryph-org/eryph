using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlCatletMetadataChangeTrackingTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletMetadataChangeTrackingTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteCatletMetadataChangeTrackingTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletMetadataChangeTrackingTests(databaseFixture, outputHelper);

public abstract class CatletMetadataChangeTrackingTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : ChangeTrackingTestBase(databaseFixture, outputHelper)
{
    private static readonly Guid MetadataId = Guid.NewGuid();
    private static readonly Guid VmId = Guid.NewGuid();
    private readonly Resources.Machines.CatletMetadata _expectedMetadata = new()
    {
        Id = MetadataId,
        //Parent = "acme-org/acme-linux/starter",
        VmId = VmId,
    };

    [Fact]
    public async Task Metadata_new_is_detected()
    {
        var newMetadataId = Guid.NewGuid();
        var newMetadata = new Resources.Machines.CatletMetadata()
        {
            Id = newMetadataId,
            //Parent = "acme-org/acme-unix/legacy",
        };
        await WithHostScope(async stateStore =>
        {
            var dbMetadata = new CatletMetadata()
            {
                Id = newMetadataId,
                Metadata = JsonSerializer.Serialize(newMetadata),
            };
            await stateStore.For<CatletMetadata>().AddAsync(dbMetadata);

            await stateStore.SaveChangesAsync();
        });

        var metadata = await ReadMetadata(newMetadataId);
        metadata.Should().BeEquivalentTo(newMetadata);
    }

    [Fact]
    public async Task Metadata_update_is_detected()
    {
        var newVmId = Guid.NewGuid();
        await WithHostScope(async stateStore =>
        {
            var dbMetadata = await stateStore.For<CatletMetadata>().GetByIdAsync(MetadataId);
            UpdateMetadata(dbMetadata!, m => m.VmId = newVmId);

            await stateStore.SaveChangesAsync();
        });

        var metadata = await ReadMetadata(MetadataId);
        _expectedMetadata.VmId = newVmId;
        metadata.Should().BeEquivalentTo(_expectedMetadata);
    }

    [Fact]
    public async Task Metadata_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var dbMetadata = await stateStore.For<CatletMetadata>().GetByIdAsync(MetadataId);
            UpdateMetadata(dbMetadata!, m => m.VmId = Guid.NewGuid());

            await stateStore.SaveChangesAsync();
        });
        MockFileSystem.File.Exists(GetMetadataPath(MetadataId)).Should().BeTrue();

        await WithHostScope(async stateStore =>
        {
            var dbMetadata = await stateStore.For<CatletMetadata>().GetByIdAsync(MetadataId);
            await stateStore.For<CatletMetadata>().DeleteAsync(dbMetadata!);

            await stateStore.SaveChangesAsync();
        });

        MockFileSystem.File.Exists(GetMetadataPath(MetadataId)).Should().BeFalse();
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata()
        {
            Id = MetadataId,
            Metadata = JsonSerializer.Serialize(_expectedMetadata),
        });
    }

    private string GetMetadataPath(Guid metadataId) =>
        Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json");

    private async Task<Resources.Machines.CatletMetadata> ReadMetadata(Guid metadataId)
    {
        var json = await MockFileSystem.File.ReadAllTextAsync(GetMetadataPath(metadataId), Encoding.UTF8);
        return JsonSerializer.Deserialize<Resources.Machines.CatletMetadata>(json)!;
    }

    private void UpdateMetadata(
        CatletMetadata dbMetadata,
        Action<Resources.Machines.CatletMetadata> update)
    {
        var metadata = JsonSerializer.Deserialize<Resources.Machines.CatletMetadata>(
            dbMetadata.Metadata)!;
        update(metadata);
        dbMetadata.Metadata = JsonSerializer.Serialize(metadata);
    }
}