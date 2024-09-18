using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using LanguageExt;

namespace Eryph.StateDb.Tests;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlGeneInventoryQueriesTests(MySqlFixture databaseFixture)
    : GeneInventoryQueriesTests(databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteGeneInventoryQueriesTests(SqliteFixture databaseFixture)
    : GeneInventoryQueriesTests(databaseFixture);

public abstract class GeneInventoryQueriesTests(IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture)
{
    private const string AgentName = "testhost";

    private const string UsedFodderGeneId = "b00e3e82-6516-418d-980f-c90cbc29a23f";
    private const string UsedVolumeGeneId = "e900670c-ba05-483b-8761-1b08a292b032";
    private const string UnusedFodderGeneId = "19355343-e1f3-4aaf-8d0e-90bec385788a";
    private const string UnusedVolumeGeneId = "32000fca-4cf5-4a5f-b6d4-72c617d296ae";

    private static readonly Guid CatletIdId = Guid.NewGuid();
    private static readonly Guid CatletMetadataId = Guid.NewGuid();
    private static readonly Guid GeneDiskId = Guid.NewGuid();
    private static readonly Guid DiskId = Guid.NewGuid();

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UsedFodderGeneId),
            GeneId = "gene:acme/acme-fodder/1.0:used-food",
            GeneType = GeneType.Fodder,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 42,
            Hash = "12345678",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UnusedFodderGeneId),
            GeneId = "gene:acme/acme-fodder/1.0:unused-food",
            GeneType = GeneType.Fodder,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 43,
            Hash = "87654321",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UsedVolumeGeneId),
            GeneId = "gene:acme/acme-os/1.0:sda",
            GeneType = GeneType.Volume,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 4200,
            Hash = "abcdef",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UnusedVolumeGeneId),
            GeneId = "gene:acme/acme-os/1.0:sdb",
            GeneType = GeneType.Volume,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 4300,
            Hash = "fedcba",
        });

        await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
        {
            Id = CatletMetadataId,
            Genes =
            [
                new CatletMetadataGene
                    {
                        MetadataId = CatletMetadataId,
                        GeneId = "gene:acme/acme-fodder/1.0:used-food",
                    }
            ],
        });

        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = CatletIdId,
            Name = "test-catlet",
            MetadataId = CatletMetadataId,
            AgentName = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = GeneDiskId,
            Name = "sda",
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            StorageIdentifier = "gene:acme/acme-os/1.0:sda",
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = DiskId,
            Name = "sda",
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            ParentId = GeneDiskId,
        });
    }

    [Theory]
    [InlineData(UsedFodderGeneId)]
    [InlineData(UsedVolumeGeneId)]
    public async Task IsUnusedGene_GeneIsUsed_ReturnsFalse(string geneId)
    {
        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.IsUnusedGene(new Guid(geneId));

            result.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(UnusedFodderGeneId)]
    [InlineData(UnusedVolumeGeneId)]
    public async Task IsUnusedGene_GeneIsUnused_ReturnsTrue(string geneId)
    {
        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.IsUnusedGene(new Guid(geneId));

            result.Should().BeTrue();
        });
    }

    [Fact]
    public async Task FindUnusedGenes_ReturnsUnusedGenes()
    {
        await WithScope(async (geneRepository, _) =>
        {
            var genes = await geneRepository.FindUnusedGenes(AgentName);

            genes.Should().SatisfyRespectively(
                gene =>
                {
                    gene.Id.Should().Be(UnusedFodderGeneId);
                    gene.GeneId.Should().Be("gene:acme/acme-fodder/1.0:unused-food");
                    gene.GeneType.Should().Be(GeneType.Fodder);
                    gene.Size.Should().Be(43);
                    gene.Hash.Should().Be("87654321");
                },
                gene =>
                {
                    gene.Id.Should().Be(UnusedVolumeGeneId);
                    gene.GeneId.Should().Be("gene:acme/acme-os/1.0:sdb");
                    gene.GeneType.Should().Be(GeneType.Volume);
                    gene.Size.Should().Be(4300);
                    gene.Hash.Should().Be("fedcba");
                });
        });
    }

    [Fact]
    public async Task GetCatletsUsingGene_GeneIsUsed_ReturnsCatlet()
    {
        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetCatletsUsingGene(
                AgentName, GeneIdentifier.New("gene:acme/acme-fodder/1.0:used-food"));

            result.Should().Equal(CatletIdId);
        });
    }

    [Fact]
    public async Task GetCatletsUsingGene_GeneIsUnused_ReturnsNothing()
    {
        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetCatletsUsingGene(
                AgentName, GeneIdentifier.New("gene:acme/acme-fodder/1.0:unused-food"));

            result.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task GetDiskUsingGene_GeneIsUsed_ReturnsDisk()
    {
        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetDisksUsingGene(
                AgentName, GeneIdentifier.New("gene:acme/acme-os/1.0:sda"));

            result.Should().Equal(DiskId);
        });
    }

    [Fact]
    public async Task GetDisksUsingGene_GeneIsUnused_ReturnsNothing()
    {
        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetDisksUsingGene(
                AgentName, GeneIdentifier.New("gene:acme/acme-os/1.0:sdb"));

            result.Should().BeEmpty();
        });
    }

    private async Task WithScope(Func<IGeneInventoryQueries, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var geneRepository = scope.GetInstance<IGeneInventoryQueries>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(geneRepository, stateStore);
    }
}
