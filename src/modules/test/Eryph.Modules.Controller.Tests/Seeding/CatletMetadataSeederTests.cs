using Eryph.Modules.Controller.Tests.ChangeTracking;
using Eryph.Modules.Controller.Tests.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using IdGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Modules.Controller.Serializers;
using Eryph.Serializers;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Seeding;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlCatletMetadataSeederTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletMetadataSeederTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteCatletMetadataSeederTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletMetadataSeederTests(databaseFixture, outputHelper);

public abstract class CatletMetadataSeederTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : SeederTestBase(databaseFixture, outputHelper)
{
    [Fact]
    public async Task Metadata_is_seeded()
    {
        var metadataId = CatletMetadataConfigModelTestData.Metadata.Id;
        await MockFileSystem.File.WriteAllTextAsync(
            Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json"),
            CatletMetadataConfigModelTestData.MetadataJson);

        await ExecuteSeeder();

        await WithScope(async stateStore =>
        {
            var metadata = await stateStore.Read<CatletMetadata>().GetByIdAsync(metadataId);
            metadata.Should().NotBeNull();
            metadata!.Id.Should().Be(CatletMetadataConfigModelTestData.Metadata.Id);
            metadata.CatletId.Should().Be(CatletMetadataConfigModelTestData.Metadata.CatletId);
            metadata.VmId.Should().Be(CatletMetadataConfigModelTestData.Metadata.VmId);
            metadata.IsDeprecated.Should().BeFalse();
            metadata.SecretDataHidden.Should().BeTrue();
            metadata.Genes.Should().HaveCount(2);
            metadata.Metadata.Should().BeEquivalentTo(CatletMetadataConfigModelTestData.Content);
            metadata.SpecificationId.Should().Be(CatletMetadataConfigModelTestData.Metadata.SpecificationId);
            metadata.SpecificationVersionId.Should().Be(CatletMetadataConfigModelTestData.Metadata.SpecificationVersionId);
        });

        var backupContent = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json.bak"));
        backupContent.Should().Be(CatletMetadataConfigModelTestData.MetadataJson);

        var configModelJson = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json"));
        var configModel = CatletMetadataConfigModelJsonSerializer.Deserialize(JsonDocument.Parse(configModelJson));
        configModel.Version.Should().Be(2);
        configModel.Id.Should().Be(metadataId);
        configModel.CatletId.Should().Be(CatletMetadataConfigModelTestData.Metadata.CatletId);
        configModel.VmId.Should().Be(CatletMetadataConfigModelTestData.Metadata.VmId);
        configModel.IsDeprecated.Should().BeFalse();
        configModel.SecretDataHidden.Should().BeTrue();
        configModel.Metadata.HasValue.Should().BeTrue();
        configModel.SpecificationId.Should().Be(CatletMetadataConfigModelTestData.Metadata.SpecificationId);
        configModel.SpecificationVersionId.Should().Be(CatletMetadataConfigModelTestData.Metadata.SpecificationVersionId);
        var content = CatletMetadataContentJsonSerializer.Deserialize(configModel.Metadata!.Value);
        content.Should().BeEquivalentTo(CatletMetadataConfigModelTestData.Content);
    }

    [Fact]
    public async Task Deprecated_metadata_is_seeded()
    {
        var metadataId = Guid.Parse("1c652dbb-aaf1-49b5-a07d-8d422c42123f");
        var catletId = Guid.Parse("4be86789-4e1d-4c19-ab4c-21c943643555");
        var vmId = Guid.Parse("99c58ef6-2208-4046-be3e-ece1d56a073a");
        var json = $$"""
                     {
                       "Id": "{{ metadataId }}",
                       "VMId": "{{ vmId }}",
                       "MachineId": "{{ catletId}}",
                       "SecureDataHidden": true,
                       "Invalid": []
                     }
                     """;

        await MockFileSystem.File.WriteAllTextAsync(
            Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json"),
            json);

        await ExecuteSeeder();

        await WithScope(async stateStore =>
        {
            var metadata = await stateStore.Read<CatletMetadata>().GetByIdAsync(metadataId);
            metadata.Should().NotBeNull();
            metadata!.Id.Should().Be(metadataId);
            metadata.CatletId.Should().Be(catletId);
            metadata.VmId.Should().Be(vmId);
            metadata.IsDeprecated.Should().BeTrue();
            metadata.SecretDataHidden.Should().BeTrue();
            metadata.Genes.Should().BeEmpty();
            metadata.Metadata.Should().BeNull();
        });

        var backupContent = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json.bak"));
        backupContent.Should().Be(json);

        var configModelJson = await MockFileSystem.File.ReadAllTextAsync(
            Path.Combine(ChangeTrackingConfig.VirtualMachinesConfigPath, $"{metadataId}.json"));
        var configModel = CatletMetadataConfigModelJsonSerializer.Deserialize(JsonDocument.Parse(configModelJson));
        configModel.Version.Should().Be(2);
        configModel.Id.Should().Be(metadataId);
        configModel.CatletId.Should().Be(catletId);
        configModel.VmId.Should().Be(vmId);
        configModel.IsDeprecated.Should().BeTrue();
        configModel.SecretDataHidden.Should().BeTrue();
        configModel.Metadata.Should().BeNull();
        configModel.SpecificationId.Should().BeNull();
        configModel.SpecificationVersionId.Should().BeNull();
    }

    private async Task ExecuteSeeder()
    {
        using var host = CreateHost();
        await host.StartAsync();
        await host.StopAsync();
    }

    protected async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(stateStore);
    }
}
