using System;
using System.Net;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;

public class OpenSshChannelTests(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    private const string RemoteAccessScope = "compute:catlets:remote-access";
    private const string PublicKey = "ssh-ed25519 AAAAExampleOperatorKey operator@host";

    [Fact]
    public async Task OpenSshChannel_WithKeyAndTtl_DispatchesCommandWithExpiry()
    {
        var before = DateTimeOffset.UtcNow;

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PutAsync(
                $"v1/catlets/{CatletId}/ssh-channel?publicKey={Uri.EscapeDataString(PublicKey)}&ttl=600",
                null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<OpenSshChannelCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.CatletId.Should().Be(CatletId);
            m.SubjectId.Should().Be(EryphConstants.SystemClientId.ToString());
            m.PublicKey.Should().Be(PublicKey);
            m.KeyExpiry.Should().NotBeNull();
            m.KeyExpiry!.Value.Should().BeCloseTo(before.AddSeconds(600), TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task OpenSshChannel_WithoutKey_DispatchesCommandWithoutKeyOrExpiry()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PutAsync($"v1/catlets/{CatletId}/ssh-channel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<OpenSshChannelCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.PublicKey.Should().BeNull();
            m.KeyExpiry.Should().BeNull();
        });
    }

    [Fact]
    public async Task OpenSshChannel_TtlWithoutKey_IsIgnored()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PutAsync($"v1/catlets/{CatletId}/ssh-channel?ttl=600", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<OpenSshChannelCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.PublicKey.Should().BeNull();
            // The TTL only applies to an injected key; without a key there is no expiry to set.
            m.KeyExpiry.Should().BeNull();
        });
    }

    [Fact]
    public async Task OpenSshChannel_WithoutRemoteAccessScope_IsForbidden()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .PutAsync($"v1/catlets/{CatletId}/ssh-channel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Factory.GetPendingRebusMessages<OpenSshChannelCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task OpenSshChannel_WithReaderProjectAccess_IsAccepted()
    {
        // remote-access requires only read project access (not write); a Reader with the scope is allowed.
        await ArrangeOtherUserAccess(BuiltinRole.Reader, EryphConstants.DefaultProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, RemoteAccessScope, false)
            .PutAsync($"v1/catlets/{CatletId}/ssh-channel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Factory.GetPendingRebusMessages<OpenSshChannelCommand>().Should().ContainSingle();
    }

    [Fact]
    public async Task OpenSshChannel_WhenCatletInOtherProject_ReturnsNotFound()
    {
        // Caller has access to another project but the catlet lives in the default project.
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, RemoteAccessScope, false)
            .PutAsync($"v1/catlets/{CatletId}/ssh-channel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.GetPendingRebusMessages<OpenSshChannelCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task OpenSshChannel_TtlExceedsMaximum_ReturnsBadRequest()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PutAsync(
                $"v1/catlets/{CatletId}/ssh-channel?publicKey={Uri.EscapeDataString(PublicKey)}&ttl=2592001",
                null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<OpenSshChannelCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task OpenSshChannel_PublicKeyTooLong_ReturnsBadRequest()
    {
        var longKey = "ssh-ed25519 " + new string('A', 2100);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PutAsync(
                $"v1/catlets/{CatletId}/ssh-channel?publicKey={Uri.EscapeDataString(longKey)}",
                null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<OpenSshChannelCommand>().Should().BeEmpty();
    }
}
