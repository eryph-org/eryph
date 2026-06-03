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

public class SshKeyTests(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    private const string RemoteAccessScope = "compute:catlets:remote-access";
    private const string PublicKey = "ssh-ed25519 AAAAExampleOperatorKey operator@host";

    [Fact]
    public async Task AddSshKey_WithExpiry_DispatchesCommand()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = PublicKey, ttl = "PT8H", expiresAt },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<AddSshKeyCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.CatletId.Should().Be(CatletId);
            m.SubjectId.Should().Be(EryphConstants.SystemClientId.ToString());
            m.PublicKey.Should().Be(PublicKey);
            m.KeyExpiry.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        });
    }

    [Fact]
    public async Task AddSshKey_WithoutExpiry_DispatchesCommandWithoutExpiry()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = PublicKey },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<AddSshKeyCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.PublicKey.Should().Be(PublicKey);
            m.KeyExpiry.Should().BeNull();
        });
    }

    [Fact]
    public async Task AddSshKey_WithoutRemoteAccessScope_IsForbidden()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = PublicKey },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.GetPendingRebusMessages<AddSshKeyCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSshKey_DispatchesCommand()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .DeleteAsync($"v1/catlets/{CatletId}/ssh-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var messages = Factory.GetPendingRebusMessages<RemoveSshKeyCommand>();
        messages.Should().SatisfyRespectively(m =>
        {
            m.CatletId.Should().Be(CatletId);
            m.SubjectId.Should().Be(EryphConstants.SystemClientId.ToString());
        });
    }

    [Fact]
    public async Task RemoveSshKey_WithoutRemoteAccessScope_IsForbidden()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .DeleteAsync($"v1/catlets/{CatletId}/ssh-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.GetPendingRebusMessages<RemoveSshKeyCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddSshKey_WithReaderProjectAccess_IsAccepted()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Reader, EryphConstants.DefaultProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, RemoteAccessScope, false)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = PublicKey },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Factory.GetPendingRebusMessages<AddSshKeyCommand>().Should().ContainSingle();
    }

    [Fact]
    public async Task AddSshKey_WhenCatletInOtherProject_ReturnsNotFound()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, RemoteAccessScope, false)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = PublicKey },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.GetPendingRebusMessages<AddSshKeyCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddSshKey_WithEmptyPublicKey_ReturnsBadRequest()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = "" },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<AddSshKeyCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddSshKey_PublicKeyTooLong_ReturnsBadRequest()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = "ssh-ed25519 " + new string('A', 2100) },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<AddSshKeyCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddSshKey_ExpiresTooFarInFuture_ReturnsBadRequest()
    {
        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .PostAsJsonAsync(
                $"v1/catlets/{CatletId}/ssh-keys",
                new { publicKey = PublicKey, expiresAt = DateTimeOffset.UtcNow.AddDays(31) },
                options: ApiJsonSerializerOptions.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.GetPendingRebusMessages<AddSshKeyCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSshKey_WhenCatletInOtherProject_ReturnsNotFound()
    {
        await ArrangeOtherUserAccess(BuiltinRole.Contributor, OtherProjectId);

        var response = await Factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, OtherClientId, RemoteAccessScope, false)
            .DeleteAsync($"v1/catlets/{CatletId}/ssh-keys");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.GetPendingRebusMessages<RemoveSshKeyCommand>().Should().BeEmpty();
    }
}
