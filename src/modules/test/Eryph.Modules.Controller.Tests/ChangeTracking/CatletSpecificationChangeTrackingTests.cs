using System.Text;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Eryph.StateDb.Specifications;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlCatletSpecificationChangeTrackingTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletSpecificationChangeTrackingTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteCatletSpecificationChangeTrackingTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : CatletSpecificationChangeTrackingTests(databaseFixture, outputHelper);

public abstract class CatletSpecificationChangeTrackingTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : ChangeTrackingTestBase(databaseFixture, outputHelper)
{
    private static readonly Guid SpecificationId = Guid.NewGuid();
    private static readonly string Name = "test-specification";

    private static readonly Guid SpecificationVersionId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2025-01-01T01:02:03Z");
    private static readonly string Comment = "Initial version";
    private static readonly string ConfigYaml = "name: test-specification\nparent: acme/acme-os/1.0\n";
    private static readonly CatletConfig ResolvedConfig = new()
    {
        ConfigType = CatletConfigType.Specification,
        Name = "test-specification",
        Parent = "parent: acme/acme-os/1.0",
    };
    private static readonly string GeneId = "catlet::gene:acme/acme-os/1.0:catlet[any]";
    private static readonly string GeneHash = "sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c";

    [Fact]
    public async Task Specification_new_is_detected()
    {
        await CreateSpecificationAsync();
        await AssertSpecificationAsync();
    }

    [Fact]
    public async Task Specification_update_is_detected()
    {
        await CreateSpecificationAsync();
        await AssertSpecificationAsync();

        var updatedVersionId = Guid.NewGuid();
        var updatedName = "updated-specification";
        var updatedComment = "Updated version";
        var updatedConfigYaml = "name: updated-specification\nparent: acme/acme-unix/2.0\n";
        var updatedResolvedConfig = new CatletConfig
        {
            ConfigType = CatletConfigType.Specification,
            Name = "updated-specification",
            Parent = "acme/acme-unix/2.0",
        };
        var updatedCreatedAt = DateTimeOffset.Parse("2025-02-02T10:11:12Z");
        var updatedGeneId = "catlet::gene:acme/acme-unix/2.0:catlet[any]";
        var updatedGeneHash = "sha256:cb476d331140e6e28442a79f26d3a1120faf2d110659508a4415ae5ce138bbf1";

        await WithHostScope(async stateStore =>
        {
            var dbSpecification = await stateStore.For<CatletSpecification>().GetByIdAsync(SpecificationId);
            dbSpecification!.Name = updatedName;

            // Versions are considered immutable, so we create new one during the update.
            var newVersion = new CatletSpecificationVersion
            {
                Id = updatedVersionId,
                SpecificationId = SpecificationId,
                ConfigYaml = updatedConfigYaml,
                ResolvedConfig = CatletConfigJsonSerializer.Serialize(updatedResolvedConfig),
                Comment = updatedComment,
                CreatedAt = updatedCreatedAt,
                Genes =
                [
                    new CatletSpecificationVersionGene
                    {
                        SpecificationVersionId = updatedVersionId,
                        GeneType = GeneType.Catlet,
                        Architecture = "any",
                        GeneSet = "acme/acme-unix/2.0",
                        Name = "catlet",
                        Hash = updatedGeneHash,
                    }
                ]
            };

            await stateStore.For<CatletSpecificationVersion>().AddAsync(newVersion);

            await stateStore.SaveChangesAsync();
        });

        var specification = await ReadSpecification(SpecificationId);
        specification.ProjectId.Should().Be(EryphConstants.DefaultProjectId);
        specification.Name.Should().Be(updatedName);
        specification.Architecture.Should().Be(EryphConstants.DefaultArchitecture);

        var firstVersion = await ReadSpecificationVersion(SpecificationVersionId);
        firstVersion.SpecificationId.Should().Be(SpecificationId);
        firstVersion.ConfigYaml.Should().Be(ConfigYaml);
        var firstResolvedConfig = CatletConfigJsonSerializer.Deserialize(firstVersion.ResolvedConfig);
        firstResolvedConfig.Should().BeEquivalentTo(ResolvedConfig);
        firstVersion.Comment.Should().Be(Comment);
        firstVersion.CreatedAt.Should().Be(CreatedAt);
        firstVersion.PinnedGenes.Should().HaveCount(1);
        firstVersion.PinnedGenes.Should().ContainKey(GeneId).WhoseValue.Should().Be(GeneHash);

        var secondVersion = await ReadSpecificationVersion(updatedVersionId);
        secondVersion.SpecificationId.Should().Be(SpecificationId);
        secondVersion.ConfigYaml.Should().Be(updatedConfigYaml);
        var secondResolvedConfig = CatletConfigJsonSerializer.Deserialize(secondVersion.ResolvedConfig);
        secondResolvedConfig.Should().BeEquivalentTo(updatedResolvedConfig);
        secondVersion.Comment.Should().Be(updatedComment);
        secondVersion.CreatedAt.Should().Be(updatedCreatedAt);
        secondVersion.PinnedGenes.Should().HaveCount(1);
        secondVersion.PinnedGenes.Should().ContainKey(updatedGeneId).WhoseValue.Should().Be(updatedGeneHash);
    }

    [Fact]
    public async Task Specification_delete_is_detected()
    {
        await CreateSpecificationAsync();
        await AssertSpecificationAsync();

        MockFileSystem.File.Exists(GetSpecificationPath(SpecificationId)).Should().BeTrue();
        MockFileSystem.File.Exists(GetSpecificationVersionPath(SpecificationVersionId)).Should().BeTrue();

        await WithHostScope(async stateStore =>
        {
            // Our database setup requires separate deletion of the versions
            var dbSpecificationVersions = await stateStore.For<CatletSpecificationVersion>()
                .ListAsync(new CatletSpecificationVersionSpecs.ListBySpecificationId(SpecificationId));
            await stateStore.For<CatletSpecificationVersion>().DeleteRangeAsync(dbSpecificationVersions);

            var dbSpecification = await stateStore.For<CatletSpecification>().GetByIdAsync(SpecificationId);
            await stateStore.For<CatletSpecification>().DeleteAsync(dbSpecification!);
            
            await stateStore.SaveChangesAsync();
        });

        MockFileSystem.File.Exists(GetSpecificationPath(SpecificationId)).Should().BeFalse();
        MockFileSystem.File.Exists(GetSpecificationVersionPath(SpecificationVersionId)).Should().BeFalse();
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    private async Task CreateSpecificationAsync()
    {
        await WithHostScope(async stateStore =>
        {
            var dbSpecification = new CatletSpecification
            {
                Id = SpecificationId,
                ProjectId = EryphConstants.DefaultProjectId,
                Name = Name,
                Architecture = EryphConstants.DefaultArchitecture,
                Environment = EryphConstants.DefaultEnvironmentName,
                Versions =
                [
                    new CatletSpecificationVersion
                    {
                        Id =  SpecificationVersionId,
                        SpecificationId = SpecificationId,
                        ConfigYaml = ConfigYaml,
                        ResolvedConfig = CatletConfigJsonSerializer.Serialize(ResolvedConfig),
                        Comment = Comment,
                        CreatedAt = CreatedAt,
                        Genes =
                        [
                            new CatletSpecificationVersionGene
                            {
                                SpecificationVersionId = SpecificationVersionId,
                                GeneType = GeneType.Catlet,
                                Architecture = "any",
                                GeneSet = "acme/acme-os/1.0",
                                Name = "catlet",
                                Hash = GeneHash,
                            }
                        ]
                    }
                ]
            };

            await stateStore.For<CatletSpecification>().AddAsync(dbSpecification);
            await stateStore.SaveChangesAsync();
        });
    }

    private async Task AssertSpecificationAsync()
    {
        var specification = await ReadSpecification(SpecificationId);
        specification.ProjectId.Should().Be(EryphConstants.DefaultProjectId);
        specification.Name.Should().Be(Name);
        specification.Architecture.Should().Be(EryphConstants.DefaultArchitecture);
        
        var version = await ReadSpecificationVersion(SpecificationVersionId);
        version.SpecificationId.Should().Be(SpecificationId);
        version.ConfigYaml.Should().Be(ConfigYaml);
        var resolved = CatletConfigJsonSerializer.Deserialize(version.ResolvedConfig);
        resolved.Should().BeEquivalentTo(ResolvedConfig);
        version.Comment.Should().Be(Comment);
        version.CreatedAt.Should().Be(CreatedAt);
        version.PinnedGenes.Should().HaveCount(1);
        version.PinnedGenes.Should().ContainKey(GeneId).WhoseValue.Should().Be(GeneHash);
    }

    private string GetSpecificationPath(Guid specificationId) =>
        Path.Combine(ChangeTrackingConfig.CatletSpecificationsConfigPath, $"{specificationId}.json");

    private string GetSpecificationVersionPath(Guid specificationVersionId) =>
        Path.Combine(ChangeTrackingConfig.CatletSpecificationVersionsConfigPath, $"{specificationVersionId}.json");

    private async Task<CatletSpecificationConfigModel> ReadSpecification(
        Guid specificationId)
    {
        var json = await MockFileSystem.File.ReadAllTextAsync(GetSpecificationPath(specificationId), Encoding.UTF8);
        return CatletSpecificationConfigModelJsonSerializer.Deserialize(json);
    }

    private async Task<CatletSpecificationVersionConfigModel> ReadSpecificationVersion(
        Guid specificationVersionId)
    {
        var json = await MockFileSystem.File.ReadAllTextAsync(GetSpecificationVersionPath(specificationVersionId), Encoding.UTF8);
        return CatletSpecificationVersionConfigModelJsonSerializer.Deserialize(json);
    }
}
