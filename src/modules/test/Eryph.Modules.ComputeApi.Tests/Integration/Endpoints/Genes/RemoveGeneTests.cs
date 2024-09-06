using Dbosoft.Hosuto.Modules.Testing;
using Eryph.StateDb.Model;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Xunit;
using Eryph.Core;
using System.Text.Json.Serialization;
using System.Text.Json;
using Eryph.Messages.Resources.Catlets.Commands;
using FluentAssertions;
using System.Net;
using Eryph.Messages.Resources.Genes.Commands;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Genes;

public class RemoveGeneTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;
    private static readonly Guid GeneId = Guid.NewGuid();

    public RemoveGeneTests(WebModuleFactory<ComputeApiModule> factory)
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
    }

    [Theory]
    [InlineData("compute:read", true)]
    [InlineData("compute:write", false)]
    public async Task Gene_is_not_deleted_when_not_authorized(
        string scope,
        bool isSuperAdmin)
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, scope, isSuperAdmin)
            .DeleteAsync($"v1/genes/{GeneId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gene_is_deleted_when_authorized()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/genes/{GeneId}");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = _factory.GetPendingRebusMessages<RemoveGeneCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.Id.Should().Be(GeneId);
            });
    }
}
