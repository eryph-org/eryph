using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;

public class GuestServicesSettingsTests(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    private const string RemoteAccessScope = "compute:catlets:remote-access";
    private const string ReadScope = "compute:catlets:read";
    private const string ShellKey = "eryph:guest-services:shell";

    [Fact]
    public async Task SetSettings_WithShell_DispatchesWrite()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PatchAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/settings",
                new { shell = "/bin/bash" },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.CatletId.Should().Be(CatletId);
            m.Values[ShellKey].Should().Be("/bin/bash");
        });
    }

    [Fact]
    public async Task SetSettings_WithEmptyShell_DispatchesRemoval()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PatchAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/settings",
                new { shell = "" },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>();
        messages.Should().SatisfyRespectively(m => m.RemoveKeys.Should().Contain(ShellKey));
    }

    [Fact]
    public async Task SetSettings_WithNothing_ReturnsBadRequest()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PatchAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/settings",
                new { },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task SetSettings_WhenCatletInOtherProject_ReturnsNotFound()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, RemoteAccessScope, false)
            .PatchAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/settings",
                new { shell = "/bin/bash" },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task SetSettings_WithoutRemoteAccessScope_IsForbidden()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, ReadScope, true)
            .PatchAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/settings",
                new { shell = "/bin/bash" },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>().Should().BeEmpty();
    }
}
