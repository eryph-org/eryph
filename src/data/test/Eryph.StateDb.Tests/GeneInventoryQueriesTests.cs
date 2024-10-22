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

    private const string UsedFoodAnyGeneId = "b00e3e82-6516-418d-980f-c90cbc29a23f";
    private const string UsedFoodHyperVGeneId = "385db5a9-bf8c-4fe2-9367-4de97b1c66f0";
    private const string UnusedFoodAnyGeneId = "19355343-e1f3-4aaf-8d0e-90bec385788a";

    private const string UsedVolumeAnyGeneId = "bd90db10-d39d-4ab1-8e9f-5bfc33b6ebc4";
    private const string UsedVolumeHyperVGeneId = "e900670c-ba05-483b-8761-1b08a292b032";
    private const string UnusedVolumeHyperVGeneId = "32000fca-4cf5-4a5f-b6d4-72c617d296ae";

    private const string CatletAnyId = "630f62c6-00dd-4905-893e-f674b55e4626";
    private const string CatletMetadataAnyId = "67bb6a20-09fd-404a-af8c-8892c5d026a6";
    private const string CatletHyperVId = "a1bfd0c4-ad61-4d7f-8fac-b5a7528846c8";
    private const string CatletMetadataHyperVId = "7065db7d-6479-4774-a9a2-108471f93a6a";

    private const string GeneDiskAnyId = "29ff13db-0e25-43a7-9206-9b808e7e64a3";
    private const string DiskAnyId = "9f22f903-66fd-4488-9afb-90d03785f3d3";
    private const string GeneDiskHyperVId = "603b4653-58e7-4a89-9e4f-f1846a0c4d2d";
    private const string DiskHyperVId = "ca7d2ffe-6333-4320-8278-82f86349f2ad";

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UsedFoodAnyGeneId),
            GeneSet = "acme/acme-fodder/1.0",
            Name = "used-food",
            Architecture = "any",
            GeneType = GeneType.Fodder,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 42,
            Hash = "12345678",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UsedFoodHyperVGeneId),
            GeneSet = "acme/acme-fodder/1.0",
            Name = "used-food",
            Architecture = "hyperv/any",
            GeneType = GeneType.Fodder,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 43,
            Hash = "87654321",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UnusedFoodAnyGeneId),
            GeneSet = "acme/acme-fodder/1.0",
            Name = "unused-food",
            Architecture = "any",
            GeneType = GeneType.Fodder,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 44,
            Hash = "56787654",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UsedVolumeAnyGeneId),
            GeneSet = "acme/acme-os/1.0",
            Name = "sda",
            Architecture = "any",
            GeneType = GeneType.Volume,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 4200,
            Hash = "abcdef",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UsedVolumeHyperVGeneId),
            GeneSet = "acme/acme-os/1.0",
            Name = "sda",
            Architecture = "hyperv/amd64",
            GeneType = GeneType.Volume,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 4300,
            Hash = "fedcba",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = new Guid(UnusedVolumeHyperVGeneId),
            GeneSet = "acme/acme-os/1.0",
            Name = "sdb",
            Architecture = "hyperv/amd64",
            GeneType = GeneType.Volume,
            LastSeenAgent = AgentName,
            LastSeen = DateTimeOffset.UtcNow,
            Size = 4400,
            Hash = "abcdcb",
        });

        await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
        {
            Id = new Guid(CatletMetadataAnyId),
            Genes =
            [
                new CatletMetadataGene
                {
                    MetadataId = new Guid(CatletMetadataAnyId),
                    GeneSet = "acme/acme-fodder/1.0",
                    Name = "used-food",
                    Architecture = "any",
                }
            ],
        });

        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = new Guid(CatletAnyId),
            Name = "test-catlet-any",
            MetadataId = new Guid(CatletMetadataAnyId),
            AgentName = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
        });

        await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
        {
            Id = new Guid(CatletMetadataHyperVId),
            Genes =
            [
                new CatletMetadataGene
                {
                    MetadataId = new Guid(CatletMetadataHyperVId),
                    GeneSet = "acme/acme-fodder/1.0",
                    Name = "used-food",
                    Architecture = "hyperv/any",
                }
            ],
        });

        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = new Guid(CatletHyperVId),
            Name = "test-catlet-hyperv",
            MetadataId = new Guid(CatletMetadataHyperVId),
            AgentName = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = new Guid(GeneDiskAnyId),
            Name = "sda",
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            GeneSet = "acme/acme-os/1.0",
            GeneName = "sda",
            GeneArchitecture = "any",
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = new Guid(DiskAnyId),
            Name = "sda",
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            ParentId = new Guid(GeneDiskAnyId),
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = new Guid(GeneDiskHyperVId),
            Name = "sda",
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            GeneSet = "acme/acme-os/1.0",
            GeneName = "sda",
            GeneArchitecture = "hyperv/amd64",
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = new Guid(DiskHyperVId),
            Name = "sda",
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            ParentId = new Guid(GeneDiskHyperVId),
        });
    }

    [Theory]
    [InlineData(UsedFoodAnyGeneId)]
    [InlineData(UsedFoodHyperVGeneId)]
    [InlineData(UsedVolumeAnyGeneId)]
    [InlineData(UsedVolumeHyperVGeneId)]
    public async Task IsUnusedGene_GeneIsUsed_ReturnsFalse(string geneId)
    {
        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.IsUnusedGene(new Guid(geneId));

            result.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(UnusedFoodAnyGeneId)]
    [InlineData(UnusedVolumeHyperVGeneId)]
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
                    gene.Id.Should().Be(UnusedFoodAnyGeneId);
                    gene.GeneSet.Should().Be("acme/acme-fodder/1.0");
                    gene.Name.Should().Be("unused-food");
                    gene.Architecture.Should().Be("any");
                    gene.GeneType.Should().Be(GeneType.Fodder);
                    gene.Size.Should().Be(44);
                    gene.Hash.Should().Be("56787654");
                },
                gene =>
                {
                    gene.Id.Should().Be(UnusedVolumeHyperVGeneId);
                    gene.GeneSet.Should().Be("acme/acme-os/1.0");
                    gene.Name.Should().Be("sdb");
                    gene.Architecture.Should().Be("hyperv/amd64");
                    gene.GeneType.Should().Be(GeneType.Volume);
                    gene.Size.Should().Be(4400);
                    gene.Hash.Should().Be("abcdcb");
                });
        });
    }

    [Theory]
    [InlineData("any", CatletAnyId)]
    [InlineData("hyperv/any", CatletHyperVId)]
    public async Task GetCatletsUsingGene_GeneIsUsed_ReturnsCatlet(
        string geneArchitecture,
        string expectedCatletId)
    {
        var uniqueGeneId = new UniqueGeneIdentifier(
            GeneType.Fodder,
            GeneIdentifier.New("gene:acme/acme-fodder/1.0:used-food"),
            Architecture.New(geneArchitecture));

        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetCatletsUsingGene(
                AgentName, uniqueGeneId);

            result.Should().Equal(new Guid(expectedCatletId));
        });
    }

    [Fact]
    public async Task GetCatletsUsingGene_GeneIsUnused_ReturnsNothing()
    {
        var uniqueGeneId = new UniqueGeneIdentifier(
            GeneType.Fodder,
            GeneIdentifier.New("gene:acme/acme-fodder/1.0:unused-food"),
            Architecture.New("any"));

        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetCatletsUsingGene(
                AgentName, uniqueGeneId);

            result.Should().BeEmpty();
        });
    }

    [Theory]
    [InlineData("any", DiskAnyId)]
    [InlineData("hyperv/amd64", DiskHyperVId)]
    public async Task GetDiskUsingGene_GeneIsUsed_ReturnsDisk(
        string geneArchitecture,
        string expectedDiskId)
    {
        var uniqueGeneId = new UniqueGeneIdentifier(
            GeneType.Fodder,
            GeneIdentifier.New("gene:acme/acme-os/1.0:sda"),
            Architecture.New(geneArchitecture));

        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetDisksUsingGene(
                AgentName, uniqueGeneId);

            result.Should().Equal(new Guid(expectedDiskId));
        });
    }

    [Fact]
    public async Task GetDisksUsingGene_GeneIsUnused_ReturnsNothing()
    {
        var uniqueGeneId = new UniqueGeneIdentifier(
            GeneType.Fodder,
            GeneIdentifier.New("gene:acme/acme-os/1.0:sdb"),
            Architecture.New("hyperv/amd64"));

        await WithScope(async (geneRepository, _) =>
        {
            var result = await geneRepository.GetDisksUsingGene(
                AgentName, uniqueGeneId);

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
