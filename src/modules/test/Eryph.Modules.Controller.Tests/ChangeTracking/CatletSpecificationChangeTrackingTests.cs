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
    private static readonly string ContentType = "application/yaml";
    private static readonly string OriginalConfig = "name: test-specification\nparent: acme/acme-os/1.0\n";

    private static readonly Guid SpecificationVersionVariantId = Guid.NewGuid();
    private static readonly CatletConfig BuiltConfig = new()
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
        var updatedVersionVariantId = Guid.NewGuid();
        var updatedName = "updated-specification";
        var updatedComment = "Updated version";
        var updatedContentType = "application/yaml";
        var updatedOriginalConfig = "name: updated-specification\nparent: acme/acme-unix/2.0\n";
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
                ContentType = updatedContentType,
                Configuration = updatedOriginalConfig,
                Architectures = new HashSet<Architecture>([Architecture.New(EryphConstants.DefaultArchitecture)]),
                Comment = updatedComment,
                CreatedAt = updatedCreatedAt,
                Variants = 
                [
                    new CatletSpecificationVersionVariant
                    {
                        Id = updatedVersionVariantId,
                        SpecificationVersionId = updatedVersionId,
                        Architecture = new Architecture(EryphConstants.DefaultArchitecture),
                        BuiltConfig = CatletConfigJsonSerializer.Serialize(updatedResolvedConfig),
                        PinnedGenes =
                        [
                            new CatletSpecificationVersionVariantGene
                            {
                                SpecificationVersionVariantId = updatedVersionId,
                                GeneType = GeneType.Catlet,
                                Architecture = "any",
                                GeneSet = "acme/acme-unix/2.0",
                                Name = "catlet",
                                Hash = updatedGeneHash,
                            }
                        ]
                    }
                ],
            };

            await stateStore.For<CatletSpecificationVersion>().AddAsync(newVersion);

            await stateStore.SaveChangesAsync();
        });

        var specification = await ReadSpecification(SpecificationId);
        specification.ProjectId.Should().Be(EryphConstants.DefaultProjectId);
        specification.Name.Should().Be(updatedName);
        specification.Architectures.Should().Equal(EryphConstants.DefaultArchitecture);

        var firstVersion = await ReadSpecificationVersion(SpecificationVersionId);
        firstVersion.SpecificationId.Should().Be(SpecificationId);
        firstVersion.ContentType.Should().Be(ContentType);
        firstVersion.OriginalConfig.Should().Be(OriginalConfig);
        firstVersion.Architectures.Should().Equal(EryphConstants.DefaultArchitecture);
        firstVersion.Comment.Should().Be(Comment);
        firstVersion.CreatedAt.Should().Be(CreatedAt);
        firstVersion.Variants.Should().SatisfyRespectively(variant =>
        {
            variant.Architecture.Should().Be(EryphConstants.DefaultArchitecture);
            var resolvedConfig = CatletConfigJsonSerializer.Deserialize(variant.BuiltConfig);
            resolvedConfig.Should().BeEquivalentTo(BuiltConfig);
            variant.PinnedGenes.Should().HaveCount(1);
            variant.PinnedGenes.Should().ContainKey(GeneId).WhoseValue.Should().Be(GeneHash);
        });

        var secondVersion = await ReadSpecificationVersion(updatedVersionId);
        secondVersion.SpecificationId.Should().Be(SpecificationId);
        secondVersion.ContentType.Should().Be(updatedContentType);
        secondVersion.OriginalConfig.Should().Be(updatedOriginalConfig);
        secondVersion.Architectures.Should().Equal(EryphConstants.DefaultArchitecture);
        secondVersion.Comment.Should().Be(updatedComment);
        secondVersion.CreatedAt.Should().Be(updatedCreatedAt);
        secondVersion.Variants.Should().SatisfyRespectively(variant =>
        {
            variant.Architecture.Should().Be(EryphConstants.DefaultArchitecture);
            var resolvedConfig = CatletConfigJsonSerializer.Deserialize(variant.BuiltConfig);
            resolvedConfig.Should().BeEquivalentTo(updatedResolvedConfig);
            variant.PinnedGenes.Should().HaveCount(1);
            variant.PinnedGenes.Should().ContainKey(updatedGeneId).WhoseValue.Should().Be(updatedGeneHash);
        });
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
                Architectures = new HashSet<Architecture>([Architecture.New(EryphConstants.DefaultArchitecture)]),
                Environment = EryphConstants.DefaultEnvironmentName,
                Versions =
                [
                    new CatletSpecificationVersion
                    {
                        Id =  SpecificationVersionId,
                        SpecificationId = SpecificationId,
                        ContentType = ContentType,
                        Configuration = OriginalConfig,
                        Architectures = new HashSet<Architecture>([Architecture.New(EryphConstants.DefaultArchitecture)]),
                        Comment = Comment,
                        CreatedAt = CreatedAt,
                        Variants =
                        [
                            new CatletSpecificationVersionVariant
                            {
                                Id = SpecificationVersionVariantId,
                                SpecificationVersionId = SpecificationVersionId,
                                Architecture = Architecture.New(EryphConstants.DefaultArchitecture),
                                BuiltConfig = CatletConfigJsonSerializer.Serialize(BuiltConfig),
                                PinnedGenes =
                                [
                                    new CatletSpecificationVersionVariantGene
                                    {
                                        SpecificationVersionVariantId = SpecificationVersionVariantId,
                                        GeneType = GeneType.Catlet,
                                        Architecture = "any",
                                        GeneSet = "acme/acme-os/1.0",
                                        Name = "catlet",
                                        Hash = GeneHash,
                                    }
                                ]
                            },
                        ],

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
        specification.Architectures.Should().Equal(EryphConstants.DefaultArchitecture);
        
        var version = await ReadSpecificationVersion(SpecificationVersionId);
        version.SpecificationId.Should().Be(SpecificationId);
        version.ContentType.Should().Be(ContentType);
        version.OriginalConfig.Should().Be(OriginalConfig);
        version.Architectures.Should().Equal(EryphConstants.DefaultArchitecture);
        version.Comment.Should().Be(Comment);
        version.CreatedAt.Should().Be(CreatedAt);
        version.Variants.Should().SatisfyRespectively(variant =>
        {
            variant.Architecture.Should().Be(EryphConstants.DefaultArchitecture);
            var builtConfig = CatletConfigJsonSerializer.Deserialize(variant.BuiltConfig);
            builtConfig.Should().BeEquivalentTo(BuiltConfig);
            variant.PinnedGenes.Should().HaveCount(1);
            variant.PinnedGenes.Should().ContainKey(GeneId).WhoseValue.Should().Be(GeneHash);
        });
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
