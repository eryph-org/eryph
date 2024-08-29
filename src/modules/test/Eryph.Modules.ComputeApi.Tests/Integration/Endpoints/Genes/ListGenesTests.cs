using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using Xunit;
using Eryph.Core;
using System.Text.Json.Serialization;
using System.Text.Json;
using FluentAssertions;
using ApiGene = Eryph.Modules.ComputeApi.Model.V1.Gene;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Genes;

public class ListGenesTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;
    private static readonly Guid FodderGeneId = Guid.NewGuid();
    private static readonly Guid VolumeGeneId = Guid.NewGuid();
    private static readonly Guid GeneSetId = Guid.NewGuid();

    public ListGenesTests(WebModuleFactory<ComputeApiModule> factory)
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
            Id = FodderGeneId,
            Name = "testgene",
            LastSeen = DateTimeOffset.UtcNow,
            GeneSetId = GeneSetId,
            Hash = "12345678",
            GeneType = GeneType.Fodder,
            Size = 42,
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = VolumeGeneId,
            Name = "testgene2",
            LastSeen = DateTimeOffset.UtcNow,
            GeneSetId = GeneSetId,
            Hash = "abcdefgh",
            GeneType = GeneType.Volume,
            Size = 43,
        });
    }

    [Fact]
    public async Task Gene_is_returned_when_authorized()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .GetAsync("v1/genes");

        response.EnsureSuccessStatusCode();

        var genes = await response.Content.ReadFromJsonAsync<ListResponse<ApiGene>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() },
            });

        genes.Value.Should().SatisfyRespectively(
            gene =>
            {
                gene!.Id.Should().Be(FodderGeneId.ToString());
                gene!.Name.Should().Be("gene:testorg/testgeneset/testtag:testgene");
                gene!.Hash.Should().Be("12345678");
                gene!.GeneType.Should().Be(GeneType.Fodder);
                gene!.Size.Should().Be(42);
            },
            gene =>
            {
                gene!.Id.Should().Be(VolumeGeneId.ToString());
                gene!.Name.Should().Be("gene:testorg/testgeneset/testtag:testgene2");
                gene!.Hash.Should().Be("abcdefgh");
                gene!.GeneType.Should().Be(GeneType.Volume);
                gene!.Size.Should().Be(43);
            });
    }
}
