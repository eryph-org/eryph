using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using LanguageExt;

namespace Eryph.StateDb.Tests;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlGeneRepositoryTests(MySqlFixture databaseFixture)
    : GeneRepositoryTests(databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteGeneRepositoryTests(SqliteFixture databaseFixture)
    : GeneRepositoryTests(databaseFixture);

public abstract class GeneRepositoryTests(IDatabaseFixture databaseFixture)
    : StateDbTestBase(databaseFixture)
{
    private const string AgentName = "testhost";
    private static readonly Guid GenePoolDiskId = Guid.NewGuid();
    private static readonly Guid UsedGeneId = Guid.NewGuid();
    private static readonly Guid UnusedGeneId = Guid.NewGuid();

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Fact]
    public async Task Foo()
    {
        await WithScope(async (geneRepository, stateStore) =>
        {
            await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
            {
                Id = GenePoolDiskId,
                ProjectId = EryphConstants.DefaultProjectId,
                Name = "sda",
                Environment = EryphConstants.DefaultEnvironmentName,
                DataStore = EryphConstants.DefaultDataStoreName,
                Geneset = "testorg/testset/testtag",
                StorageIdentifier = "gene:testorg/testset/testtag:sda",
            });

            await geneRepository.AddAsync(new Gene
            {
                Id = UsedGeneId,
                GeneSet = "testorg/testset/testtag",
                Name = "sda",
                GeneType = GeneType.Volume,
                LastSeenAgent = AgentName,
                LastSeen = DateTimeOffset.UtcNow,
                Size = 42,
                Hash = "12345678",
            });

            await geneRepository.AddAsync(new Gene
            {
                Id = UnusedGeneId,
                GeneSet = "testorg/testset/testtag",
                Name = "sdb",
                GeneType = GeneType.Volume,
                LastSeenAgent = AgentName,
                LastSeen = DateTimeOffset.UtcNow,
                Size = 43,
                Hash = "abcdef",
            });

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (geneRepository, _) =>
        {
            var genes = await geneRepository.GetUnusedVolumeGenes(AgentName);

            genes.Should().SatisfyRespectively(
                gene => gene.Id.Should().Be(UnusedGeneId));
        });
    }

    private async Task WithScope(Func<IGeneRepository, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var geneRepository = scope.GetInstance<IGeneRepository>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(geneRepository, stateStore);
    }
}
