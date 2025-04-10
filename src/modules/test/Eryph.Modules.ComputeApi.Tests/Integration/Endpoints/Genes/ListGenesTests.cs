﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;

using ApiGene = Eryph.Modules.ComputeApi.Model.V1.Gene;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Genes;

public class ListGenesTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    private const string AgentName = "testhost";
    private static readonly Guid FodderGeneId = new("77e1e6e5-3ede-4c21-ac09-fdc943e64f1d");
    private static readonly Guid VolumeGeneId = new("bcba0b8c-4ea8-4036-aaa9-b20d80931712");

    public ListGenesTests(
        ITestOutputHelper outputHelper,
        WebModuleFactory<ComputeApiModule> factory)
        : base(outputHelper)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = FodderGeneId,
            GeneSet = "acme/acme-fodder/1.0",
            Name = "test-food",
            Architecture = "any",
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = AgentName,
            Hash = "12345678",
            GeneType = GeneType.Fodder,
            Size = 42,
        });

        await stateStore.For<Gene>().AddAsync(new Gene
        {
            Id = VolumeGeneId,
            GeneSet = "acme/acme-os/1.0",
            Name = "sda",
            Architecture = "hyperv/amd64",
            LastSeen = DateTimeOffset.UtcNow,
            LastSeenAgent = AgentName,
            Hash = "abcdefgh",
            GeneType = GeneType.Volume,
            Size = 43,
        });
    }

    [Fact]
    public async Task Gene_is_returned_when_authorized()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", false)
            .GetAsync("v1/genes");

        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var genes = await response.Content.ReadFromJsonAsync<ListResponse<ApiGene>>(
            options: ApiJsonSerializerOptions.Options);

        genes.Value.Should().SatisfyRespectively(
            gene =>
            {
                gene.Id.Should().Be(FodderGeneId.ToString());
                gene.GeneSet.Should().Be("acme/acme-fodder/1.0");
                gene.Name.Should().Be("test-food");
                gene.Architecture.Should().Be("any");
                gene.Hash.Should().Be("12345678");
                gene.GeneType.Should().Be(GeneType.Fodder);
                gene.Size.Should().Be(42);
            },
            gene =>
            {
                gene.Id.Should().Be(VolumeGeneId.ToString());
                gene.GeneSet.Should().Be("acme/acme-os/1.0");
                gene.Name.Should().Be("sda");
                gene.Architecture.Should().Be("hyperv/amd64");
                gene.Hash.Should().Be("abcdefgh");
                gene.GeneType.Should().Be(GeneType.Volume);
                gene.Size.Should().Be(43);
            });
    }
}
