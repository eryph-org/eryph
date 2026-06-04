using System.Net;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;

public class GuestServicesStatusTests(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    private const string ReadScope = "compute:catlets:read";
    private const string RemoteAccessScope = "compute:catlets:remote-access";

    [Fact]
    public async Task GetGuestServicesStatus_DispatchesCommand()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, ReadScope, true)
            .GetAsync($"v1/catlets/{CatletId}/guest-services");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<GetGuestServicesStatusCommand>();
        messages.Should().SatisfyRespectively(m => m.CatletId.Should().Be(CatletId));
    }

    [Fact]
    public async Task GetGuestServicesStatus_WithoutReadScope_IsForbidden()
    {
        // remote-access does not imply read (the scope hierarchy is flattened),
        // so a remote-access-only token cannot read the status.
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .GetAsync($"v1/catlets/{CatletId}/guest-services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.GetPendingRebusMessages<GetGuestServicesStatusCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task GetGuestServicesStatus_WhenCatletInOtherProject_ReturnsNotFound()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Reader, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, ReadScope, false)
            .GetAsync($"v1/catlets/{CatletId}/guest-services");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.GetPendingRebusMessages<GetGuestServicesStatusCommand>().Should().BeEmpty();
    }
}
