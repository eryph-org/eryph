using Dbosoft.Hosuto.Modules.Testing;
using Eryph.StateDb.TestBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb;
using Xunit;
using Eryph.Core;
using FluentAssertions;
using System.Net;
using Eryph.Messages.Genes.Commands;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Genes;

public class CleanupGenesTests : InMemoryStateDbTestBase, IClassFixture<WebModuleFactory<ComputeApiModule>>
{
    private readonly WebModuleFactory<ComputeApiModule> _factory;

    public CleanupGenesTests(
        ITestOutputHelper outputHelper,
        WebModuleFactory<ComputeApiModule> factory)
        : base(outputHelper)
    {
        _factory = factory.WithApiHost(ConfigureDatabase);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    [Theory]
    [InlineData("compute:read", true)]
    [InlineData("compute:write", false)]
    public async Task Genes_are_not_cleaned_up_when_not_authorized(
        string scope,
        bool isSuperAdmin)
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, scope, isSuperAdmin)
            .DeleteAsync("v1/genes");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Genes_are_cleaned_up_when_authorized()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:write", true)
            .DeleteAsync("v1/genes");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = _factory.GetPendingRebusMessages<CleanupGenesCommand>();
        messages.Should().HaveCount(1);
    }
}
