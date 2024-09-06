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
    private static readonly Guid FodderGeneId = new("77e1e6e5-3ede-4c21-ac09-fdc943e64f1d");
    private static readonly Guid VolumeGeneId = new("bcba0b8c-4ea8-4036-aaa9-b20d80931712");

    public ListGenesTests(WebModuleFactory<ComputeApiModule> factory)
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
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = "testhost",
            Hash = "12345678",
            GeneType = GeneType.Fodder,
            Size = 42,
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = VolumeGeneId,
            GeneId = "gene:acme/acme-os/1.0:sda",
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = "testhost",
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
                gene!.Name.Should().Be("gene:acme/acme-fodder/1.0:test-food");
                gene!.Hash.Should().Be("12345678");
                gene!.GeneType.Should().Be(GeneType.Fodder);
                gene!.Size.Should().Be(42);
            },
            gene =>
            {
                gene!.Id.Should().Be(VolumeGeneId.ToString());
                gene!.Name.Should().Be("gene:acme/acme-os/1.0:sda");
                gene!.Hash.Should().Be("abcdefgh");
                gene!.GeneType.Should().Be(GeneType.Volume);
                gene!.Size.Should().Be(43);
            });
    }
}
