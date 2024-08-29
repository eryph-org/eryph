using System;
using System.Collections.Generic;
using System.Linq;
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

using ApiGene = Eryph.Modules.ComputeApi.Model.V1.Gene;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Genes;

public class GetGeneTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;
    private static readonly Guid GeneId = Guid.NewGuid();
    private static readonly Guid GeneSetId = Guid.NewGuid();

    public GetGeneTests(WebModuleFactory<ComputeApiModule> factory)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<GeneSet>().AddAsync(new GeneSet
        {
            Id = GeneSetId,
            Organization = "testorg",
            Name = "testgeneset",
            Tag = "testtag",
            Hash = "abcdefgh",
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = "host",
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = GeneId,
            Name = "testgene",
            LastSeen = DateTimeOffset.UtcNow,
            GeneSetId = GeneSetId,
            Hash = "12345678",
            GeneType = GeneType.Volume,
            Size = 42,
        });
    }

    [Fact]
    public async Task Gene_is_returned_when_authorized()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .GetAsync($"v1/genes/{GeneId}");

        response.EnsureSuccessStatusCode();
        
        var gene = await response.Content.ReadFromJsonAsync<ApiGene>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() },
            });

        gene.Should().NotBeNull();
        gene!.Id.Should().Be(GeneId.ToString());
        gene!.Name.Should().Be("gene:testorg/testgeneset/testtag:testgene");
        gene!.Hash.Should().Be("12345678");
        gene!.GeneType.Should().Be(GeneType.Volume);
        gene!.Size.Should().Be(42);
    }
}
