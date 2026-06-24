using System;
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

public class AccessKeyTests(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    private const string RemoteAccessScope = "compute:catlets:remote-access";
    private const string ReadScope = "compute:catlets:read";
    private const string PublicKey = "ssh-ed25519 AAAAExampleOperatorKey operator@host";

    // The per-subject authorized-key slot the guest service reads.
    private static string AccessKeySlot =>
        "eryph:guest-services:client-public-key:" + EryphConstants.SystemClientId;

    [Fact]
    public async Task AddAccessKey_WithExpiry_DispatchesWriteWithExpiryLine()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/access-keys",
                new { publicKey = PublicKey, ttl = "PT8H", expiresAt },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.CatletId.Should().Be(CatletId);
            m.Values.Should().ContainKey(AccessKeySlot);
            m.Values[AccessKeySlot].Should().StartWith("expiry-time=").And.EndWith(PublicKey);
        });
    }

    [Fact]
    public async Task AddAccessKey_WithoutExpiry_DispatchesBareKey()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/access-keys",
                new { publicKey = PublicKey },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>();
        messages.Should().SatisfyRespectively(m => m.Values[AccessKeySlot].Should().Be(PublicKey));
    }

    [Fact]
    public async Task AddAccessKey_WithoutRemoteAccessScope_IsForbidden()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, ReadScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/access-keys",
                new { publicKey = PublicKey },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddAccessKey_WithEmptyPublicKey_ReturnsBadRequest()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/access-keys",
                new { publicKey = "" },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddAccessKey_WithPastExpiry_ReturnsBadRequest()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/guest-services/access-keys",
                new { publicKey = PublicKey, expiresAt },
                ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAccessKey_DispatchesRemoval()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .DeleteAsync($"v1/catlets/{CatletId}/guest-services/access-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.CatletId.Should().Be(CatletId);
            m.RemoveKeys.Should().ContainSingle().Which.Should().Be(AccessKeySlot);
        });
    }

    [Fact]
    public async Task RemoveAccessKey_WhenCatletInOtherProject_ReturnsNotFound()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, RemoteAccessScope, false)
            .DeleteAsync($"v1/catlets/{CatletId}/guest-services/access-keys");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.GetPendingRebusMessages<SetGuestServicesDataCommand>().Should().BeEmpty();
    }
}
