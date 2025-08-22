using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Configuration.Model;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.Serializers;
using Eryph.Resources.Machines;
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
    private static readonly Guid CatletId = Guid.NewGuid();
    private static readonly Guid VmId = Guid.NewGuid();

    private readonly CatletMetadataContent _expectedMetadataContent = new()
    {
        Architecture = Architecture.New("hyperv/amd64"),
        BuiltConfig = new CatletConfig
        {
            Parent = "acme-org/acme-linux/starter",
        },
    };

    [Fact]
    public async Task Metadata_new_is_detected()
    {
        var newMetadataId = Guid.NewGuid();
        var newCatletId = Guid.NewGuid();
        var newVmId = Guid.NewGuid();
        var newMetadata = new CatletMetadataContent()
        {
            Architecture = Architecture.New("hyperv/amd64"),
            BuiltConfig = new CatletConfig
            {
                Parent = "acme-org/acme-unix/legacy",
            },
        };

        await WithHostScope(async stateStore =>
        {
            var dbMetadata = new CatletMetadata()
            {
                Id = newMetadataId,
                CatletId = newCatletId,
                VmId = newVmId,
                Metadata = newMetadata,
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
            dbMetadata!.SecretDataHidden = true;

            await stateStore.SaveChangesAsync();
        });

        var metadata = await ReadMetadata(MetadataId);
        metadata.SecretDataHidden.Should().BeTrue();
    }

    [Fact]
    public async Task Metadata_content_update_is_detected()
    {
        var newVmId = Guid.NewGuid();
        await WithHostScope(async stateStore =>
        {
            var dbMetadata = await stateStore.For<CatletMetadata>().GetByIdAsync(MetadataId);
            dbMetadata!.Metadata!.Architecture = new Architecture("any");

            await stateStore.SaveChangesAsync();
        });

        var metadata = await ReadMetadata(MetadataId);
        var metadataContent = CatletMetadataJsonSerializer.Deserialize(metadata.Metadata);
        
        _expectedMetadataContent.Architecture = new Architecture("any");

        metadataContent.Should().BeEquivalentTo(_expectedMetadataContent);
    }


    [Fact]
    public async Task Metadata_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var dbMetadata = await stateStore.For<CatletMetadata>().GetByIdAsync(MetadataId);
            dbMetadata!.SecretDataHidden = true;

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
            CatletId = CatletId,
            VmId = VmId,
            Metadata = _expectedMetadataContent,
        });
    }

    private string GetMetadataPath(Guid metadataId) =>
        Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json");

    private async Task<CatletMetadataConfigModel> ReadMetadata(Guid metadataId)
    {
        var json = await MockFileSystem.File.ReadAllTextAsync(GetMetadataPath(metadataId), Encoding.UTF8);
        return CatletMetadataConfigModelJsonSerializer.Deserialize(JsonDocument.Parse(json));
    }
}