using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;

using ApiGene = Eryph.Modules.ComputeApi.Model.V1.GeneWithUsage;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Genes;

public class GetGeneTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;
    private static readonly Guid GeneId = Guid.NewGuid();
    private static readonly Guid DiskId = Guid.NewGuid();
    private static readonly Guid GeneDiskId = Guid.NewGuid();

    public GetGeneTests(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = GeneId,
            GeneId = "gene:acme/acme-os/1.0:sda",
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = "testhost",
            Hash = "12345678",
            GeneType = GeneType.Volume,
            Size = 42,
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = GeneDiskId,
            Name = "test-disk",
            StorageIdentifier = "gene:acme/acme-os/1.0:sda",
            Geneset = "acme/acme-os/1.0",
            LastSeenAgent = "testhost",
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            LastSeen = DateTimeOffset.UtcNow,
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = DiskId,
            Name = "other-disk",
            ParentId = GeneDiskId,
            LastSeenAgent = "testhost",
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            LastSeen = DateTimeOffset.UtcNow,
        });
    }

    [Fact]
    public async Task Gene_does_not_exist_returns_not_found()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", false)
            .GetAsync($"v1/genes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Volume_gene_is_returned_with_usage()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", false)
            .GetAsync($"v1/genes/{GeneId}");

        response.EnsureSuccessStatusCode();
        
        var gene = await response.Content.ReadFromJsonAsync<ApiGene>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() },
            });

        gene.Should().NotBeNull();
        gene!.Id.Should().Be(GeneId.ToString());
        gene!.GeneSet.Should().Be("acme/acme-os/1.0");
        gene!.Name.Should().Be("sda");
        gene!.Hash.Should().Be("12345678");
        gene!.GeneType.Should().Be(GeneType.Volume);
        gene!.Size.Should().Be(42);
        gene.Disks.Should().Equal(DiskId);
    }
}
