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
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.Serializers;
using Eryph.Serializers;
using Microsoft.Extensions.DependencyInjection;
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
            metadata.Metadata.Should().NotBeNull();
            metadata.Metadata!.Architecture.Value.Should().Be(EryphConstants.DefaultArchitecture);
            metadata.Metadata.Config.Should().NotBeNull();
            metadata.Metadata.Config.Parent.Should().BeNull();
            metadata.Metadata.PinnedGenes.Should().BeEmpty();
            metadata.Metadata.ContentType.Should().BeEmpty();
            metadata.Metadata.OriginalConfig.Should().BeEmpty();
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
        configModel.Metadata.Should().NotBeNull();
        configModel.SpecificationId.Should().BeNull();
        configModel.SpecificationVersionId.Should().BeNull();
    }

    [Fact]
    public async Task Deprecated_metadata_salvages_parent_and_architecture()
    {
        var metadataId = Guid.Parse("0b8e3f55-22a8-4f7b-9f5a-5d11d1f4f9aa");
        var catletId = Guid.Parse("ab02c8ca-2c70-4f4d-a4d6-ab1b3a90c9aa");
        var vmId = Guid.Parse("a4b1f8d2-7d8a-4a2a-9f6e-9f9f9e0aabba");
        var json = $$"""
                     {
                       "Id": "{{ metadataId }}",
                       "VMId": "{{ vmId }}",
                       "MachineId": "{{ catletId }}",
                       "Architecture": "hyperv/amd64",
                       "Parent": "dbosoft/ubuntu-22.04/starter",
                       "SecureDataHidden": false
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
            metadata!.IsDeprecated.Should().BeTrue();
            metadata.Metadata.Should().NotBeNull();
            metadata.Metadata!.Architecture.Value.Should().Be("hyperv/amd64");
            metadata.Metadata.Config.Parent.Should().Be("dbosoft/ubuntu-22.04/starter");
        });
    }

    [Fact]
    public async Task Deprecated_metadata_with_invalid_architecture_falls_back_to_default()
    {
        var metadataId = Guid.Parse("d59a52f0-6e36-4f6d-b3f9-1c8b3f2c1aaa");
        var catletId = Guid.Parse("c5e72f8a-bd05-4c25-b21a-2c8b3f2c2aaa");
        var vmId = Guid.Parse("ec9a37ee-2c0e-4c0e-9f8a-3c8b3f2c3aaa");
        var json = $$"""
                     {
                       "Id": "{{ metadataId }}",
                       "VMId": "{{ vmId }}",
                       "MachineId": "{{ catletId }}",
                       "Architecture": "not-a-real-arch",
                       "Parent": "some/parent/tag"
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
            metadata!.Metadata.Should().NotBeNull();
            metadata.Metadata!.Architecture.Value.Should().Be(EryphConstants.DefaultArchitecture);
            metadata.Metadata.Config.Parent.Should().Be("some/parent/tag");
        });
    }

    private async Task ExecuteSeeder()
    {
        using var host = CreateHost();
        await host.StartAsync();
        await ChangeTrackingTestHelpers.WaitForIdleAsync(host, TimeSpan.FromSeconds(10));
        await host.StopAsync();
    }

    protected async Task WithScope(Func<IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(stateStore);
    }
}
