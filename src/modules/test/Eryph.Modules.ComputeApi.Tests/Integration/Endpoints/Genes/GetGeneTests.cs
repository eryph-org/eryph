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
using Eryph.Modules.AspNetCore.ApiProvider;
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
    
    private const string AgentName = "testhost";
    private static readonly Guid FodderGeneId = Guid.NewGuid();
    private static readonly Guid VolumeGeneId = Guid.NewGuid();
    private static readonly Guid CatletId = Guid.NewGuid();
    private static readonly Guid CatletMetadataId = Guid.NewGuid();
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
            Id = FodderGeneId,
            GeneId = "gene:acme/acme-fodder/1.0:test-food",
            Architecture = "any",
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = AgentName,
            Hash = "12345678",
            GeneType = GeneType.Fodder,
            Size = 42,
        });

        await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata()
        {
            Id = CatletMetadataId,
            Genes =
            [
                new CatletMetadataGene
                {
                    MetadataId = CatletMetadataId,
                    GeneId = "gene:acme/acme-fodder/1.0:test-food",
                    Architecture = "any",
                },
            ],
        });

        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = CatletId,
            MetadataId = CatletMetadataId,
            Name = "test-catlet",
            AgentName = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = VolumeGeneId,
            GeneId = "gene:acme/acme-os/1.0:sda",
            Architecture = "hyperv/amd64",
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = AgentName,
            Hash = "abcdef",
            GeneType = GeneType.Volume,
            Size = 42,
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = GeneDiskId,
            Name = "sda",
            StorageIdentifier = "gene:acme/acme-os/1.0:sda",
            Geneset = "acme/acme-os/1.0",
            LastSeenAgent = AgentName,
            ProjectId = EryphConstants.DefaultProjectId,
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            LastSeen = DateTimeOffset.UtcNow,
        });

        await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
        {
            Id = DiskId,
            Name = "sda",
            ParentId = GeneDiskId,
            LastSeenAgent = AgentName,
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
    public async Task Fodder_gene_is_returned_with_usage()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", false)
            .GetAsync($"v1/genes/{FodderGeneId}");

        response.EnsureSuccessStatusCode();

        var gene = await response.Content.ReadFromJsonAsync<ApiGene>(
            options: ApiJsonSerializerOptions.Options);

        gene.Should().NotBeNull();
        gene.Id.Should().Be(FodderGeneId.ToString());
        gene.GeneSet.Should().Be("acme/acme-fodder/1.0");
        gene.Name.Should().Be("test-food");
        gene.Hash.Should().Be("12345678");
        gene.GeneType.Should().Be(GeneType.Fodder);
        gene.Size.Should().Be(42);
        gene.Catlets.Should().Equal(CatletId.ToString());
        gene.Disks.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Volume_gene_is_returned_with_usage()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", false)
            .GetAsync($"v1/genes/{VolumeGeneId}");

        response.EnsureSuccessStatusCode();

        var gene = await response.Content.ReadFromJsonAsync<ApiGene>(
            options: ApiJsonSerializerOptions.Options);

        gene.Should().NotBeNull();
        gene.Id.Should().Be(VolumeGeneId.ToString());
        gene.GeneSet.Should().Be("acme/acme-os/1.0");
        gene.Name.Should().Be("sda");
        gene.Hash.Should().Be("abcdef");
        gene.GeneType.Should().Be(GeneType.Volume);
        gene.Size.Should().Be(42);
        gene.Catlets.Should().BeNullOrEmpty();
        gene.Disks.Should().Equal(DiskId.ToString());
    }
}
