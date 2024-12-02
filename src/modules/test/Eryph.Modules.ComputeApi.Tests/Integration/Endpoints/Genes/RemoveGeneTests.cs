using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Messages.Genes.Commands;
using Eryph.StateDb.Model;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using Eryph.Core.Genetics;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Genes;

public class RemoveGeneTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;
    private static readonly Guid GeneId = Guid.NewGuid();

    public RemoveGeneTests(
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
            Id = GeneId,
            GeneSet = "acme/acme-os/1.0",
            Name = "sda",
            Architecture = "hyperv/amd64",
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

        response.Should().HaveStatusCode(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gene_is_deleted_when_authorized()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync($"v1/genes/{GeneId}");

        response.Should().HaveStatusCode(HttpStatusCode.Accepted);

        var messages = _factory.GetPendingRebusMessages<RemoveGeneCommand>();
        messages.Should().SatisfyRespectively(
            m =>
            {
                m.Id.Should().Be(GeneId);
            });
    }
}
