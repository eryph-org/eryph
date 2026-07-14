using System.Net;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;

public class ProvisioningLogTests(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    private const string ReadScope = "compute:catlets:read";
    private const string RemoteAccessScope = "compute:catlets:remote-access";

    [Fact]
    public async Task GetProvisioningLog_DispatchesCommand()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, ReadScope, true)
            .GetAsync($"v1/catlets/{CatletId}/guest-services/provisioning-log");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<GetProvisioningLogCommand>();
        messages.Should().SatisfyRespectively(m => m.CatletId.Should().Be(CatletId));
    }

    [Fact]
    public async Task GetProvisioningLog_WithoutReadScope_IsForbidden()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .GetAsync($"v1/catlets/{CatletId}/guest-services/provisioning-log");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.GetPendingRebusMessages<GetProvisioningLogCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task GetProvisioningLog_WhenCatletInOtherProject_ReturnsNotFound()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Reader, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, ReadScope, false)
            .GetAsync($"v1/catlets/{CatletId}/guest-services/provisioning-log");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.GetPendingRebusMessages<GetProvisioningLogCommand>().Should().BeEmpty();
    }
}
