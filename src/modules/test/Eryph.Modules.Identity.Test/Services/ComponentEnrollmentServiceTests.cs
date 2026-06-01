#nullable enable
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class ComponentEnrollmentServiceTests
{
    private static ComponentCertificateAuthority CreateCa() =>
        new(new InMemoryCertificateStore(), new CertificateGenerator(), new InMemoryKeyService());

    private static ComponentEnrollmentService CreateService(bool authorized) =>
        new(CreateCa(), new StubPolicy(authorized), NullLogger<ComponentEnrollmentService>.Instance);

    private static byte[] NewPublicKey()
    {
        using var key = RSA.Create(2048);
        return key.ExportSubjectPublicKeyInfo();
    }

    [Fact]
    public async Task Enroll_issues_certificate_chaining_to_the_ca_with_a_server_derived_id()
    {
        var sut = CreateService(authorized: true);

        var result = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        // The id is derived server-side, not taken from the request.
        result.ComponentId.Should().Be(
            ComponentIdentity.DeriveComponentId(ComponentType.VMHostAgent, "agent1.eryph.local"));
        result.CaTrustBundle.Should().NotBeEmpty();
        result.IssuingChain.Should().NotBeEmpty("the component must receive the intermediate to present");

        using var leaf = X509CertificateLoader.LoadCertificate(result.Certificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        foreach (var rootDer in result.CaTrustBundle)
            chain.ChainPolicy.CustomTrustStore.Add(X509CertificateLoader.LoadCertificate(rootDer));
        foreach (var intermediateDer in result.IssuingChain)
            chain.ChainPolicy.ExtraStore.Add(X509CertificateLoader.LoadCertificate(intermediateDer));
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf).Should().BeTrue(
            "the issued leaf must chain through the intermediate to a root in the returned bundle");
    }

    [Fact]
    public async Task Enroll_also_issues_a_server_certificate_when_a_server_key_is_supplied()
    {
        var sut = CreateService(authorized: true);

        var result = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = NewPublicKey(),
            ServerPublicKey = NewPublicKey(),
            ServerDnsNames = ["api.eryph.local"],
        });

        result.ServerCertificate.Should().NotBeEmpty("a server key was supplied");
        result.ServerIssuingChain.Should().NotBeEmpty();
        using var server = X509CertificateLoader.LoadCertificate(result.ServerCertificate);
        server.MatchesHostname("api.eryph.local", false, false).Should().BeTrue();
    }

    [Fact]
    public async Task Enroll_covers_every_requested_server_name_in_the_certificate()
    {
        var sut = CreateService(authorized: true);

        var result = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = NewPublicKey(),
            ServerPublicKey = NewPublicKey(),
            ServerDnsNames = ["api.eryph.local", "compute.eryph.local"],
        });

        using var server = X509CertificateLoader.LoadCertificate(result.ServerCertificate);
        server.MatchesHostname("api.eryph.local", false, false).Should().BeTrue();
        server.MatchesHostname("compute.eryph.local", false, false)
            .Should().BeTrue("every requested name must be covered, not just the first");
    }

    [Fact]
    public async Task Enroll_rejects_when_any_requested_server_name_is_invalid()
    {
        var sut = CreateService(authorized: true);

        var act = () => sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = NewPublicKey(),
            ServerPublicKey = NewPublicKey(),
            ServerDnsNames = ["api.eryph.local", "*.eryph.local"],
        });

        await act.Should().ThrowAsync<ComponentEnrollmentException>("a wildcard name must be rejected");
    }

    [Fact]
    public async Task Enroll_omits_the_server_certificate_when_no_server_key_is_supplied()
    {
        var sut = CreateService(authorized: true);

        var result = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        result.ServerCertificate.Should().BeEmpty();
    }

    [Fact]
    public async Task Enroll_throws_when_the_policy_denies_the_request()
    {
        var sut = CreateService(authorized: false);

        var act = () => sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.Identity,
            Fqdn = "id.eryph.local",
            PublicKey = NewPublicKey(),
        });

        await act.Should().ThrowAsync<ComponentEnrollmentException>();
    }

    [Fact]
    public async Task Enroll_throws_on_an_invalid_public_key()
    {
        var sut = CreateService(authorized: true);

        var act = () => sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = [1, 2, 3],
        });

        await act.Should().ThrowAsync<ComponentEnrollmentException>();
    }

    [Fact]
    public async Task TokenEnrollmentPolicy_authorizes_when_the_redeemer_accepts_the_token()
    {
        var policy = new TokenEnrollmentPolicy(
            new StubTokenRedeemer(EnrollmentTokenValidationResult.Valid(ComponentType.VMHostAgent)),
            NullLogger<TokenEnrollmentPolicy>.Instance);

        (await policy.IsAuthorizedAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Token = "token",
        })).Should().BeTrue();
    }

    [Fact]
    public async Task TokenEnrollmentPolicy_denies_when_the_redeemer_rejects_the_token()
    {
        // The redeemer enforces signature/expiry/type/one-time and returns Invalid; the policy denies.
        var policy = new TokenEnrollmentPolicy(
            new StubTokenRedeemer(EnrollmentTokenValidationResult.Invalid),
            NullLogger<TokenEnrollmentPolicy>.Instance);

        (await policy.IsAuthorizedAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Token = "token",
        })).Should().BeFalse();
    }

    [Fact]
    public async Task TokenEnrollmentPolicy_denies_when_no_token_is_presented()
    {
        var policy = new TokenEnrollmentPolicy(
            new StubTokenRedeemer(EnrollmentTokenValidationResult.Valid(ComponentType.VMHostAgent)),
            NullLogger<TokenEnrollmentPolicy>.Instance);

        (await policy.IsAuthorizedAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Token = "",
        })).Should().BeFalse();
    }

    [Fact]
    public async Task Enroll_does_not_consume_the_token_on_a_recoverable_client_error()
    {
        // Real CA + policy + redeemer over a shared in-memory IdentityDb, so the redeemed-token state
        // is shared across requests exactly as it would be against a real database.
        var ca = CreateCa();
        var root = new InMemoryDatabaseRoot();
        var dbName = "enroll-" + Guid.NewGuid().ToString("N");

        ComponentEnrollmentService NewService()
        {
            var context = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(dbName, root).Options);
            var policy = new TokenEnrollmentPolicy(
                new EnrollmentTokenRedeemer(ca, new IdentityDbRepository<RedeemedEnrollmentToken>(context)),
                NullLogger<TokenEnrollmentPolicy>.Instance);
            return new ComponentEnrollmentService(ca, policy, NullLogger<ComponentEnrollmentService>.Instance);
        }

        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.ComputeApi, "api.eryph.local", DateTimeOffset.UtcNow.AddMinutes(5));

        // A valid token with garbage client-key bytes is rejected WITHOUT burning the token...
        var badKey = () => NewService().EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = [1, 2, 3],
            Token = token,
        });
        await badKey.Should().ThrowAsync<ComponentEnrollmentException>();

        // ...and so is a valid token with an invalid requested server name.
        var badServerName = () => NewService().EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = NewPublicKey(),
            ServerPublicKey = NewPublicKey(),
            ServerDnsNames = ["*.eryph.local"],
            Token = token,
        });
        await badServerName.Should().ThrowAsync<ComponentEnrollmentException>();

        // ...so the corrected retry still succeeds: the one-time token survived the recoverable errors.
        var result = await NewService().EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = NewPublicKey(),
            Token = token,
        });
        result.ComponentId.Should().Be(
            ComponentIdentity.DeriveComponentId(ComponentType.ComputeApi, "api.eryph.local"));

        // The token is now spent; a further attempt is rejected.
        var replay = () => NewService().EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = NewPublicKey(),
            Token = token,
        });
        await replay.Should().ThrowAsync<ComponentEnrollmentException>();
    }

    [Fact]
    public async Task Enroll_rejects_a_token_bound_to_a_different_host_without_consuming_it()
    {
        var ca = CreateCa();
        var root = new InMemoryDatabaseRoot();
        var dbName = "enroll-fqdn-" + Guid.NewGuid().ToString("N");

        ComponentEnrollmentService NewService()
        {
            var context = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(dbName, root).Options);
            var policy = new TokenEnrollmentPolicy(
                new EnrollmentTokenRedeemer(ca, new IdentityDbRepository<RedeemedEnrollmentToken>(context)),
                NullLogger<TokenEnrollmentPolicy>.Instance);
            return new ComponentEnrollmentService(ca, policy, NullLogger<ComponentEnrollmentService>.Instance);
        }

        // Token is bound to api.eryph.local.
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.ComputeApi, "api.eryph.local", DateTimeOffset.UtcNow.AddMinutes(5));

        // A host presenting a different FQDN is rejected and does NOT consume the token...
        var wrongHost = () => NewService().EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "other.eryph.local",
            PublicKey = NewPublicKey(),
            Token = token,
        });
        await wrongHost.Should().ThrowAsync<ComponentEnrollmentException>();

        // ...so the bound host can still enroll with it.
        var result = await NewService().EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = NewPublicKey(),
            Token = token,
        });
        result.ComponentId.Should().Be(
            ComponentIdentity.DeriveComponentId(ComponentType.ComputeApi, "api.eryph.local"));
    }

    private sealed class StubPolicy(bool authorized) : IComponentEnrollmentPolicy
    {
        public Task<bool> IsAuthorizedAsync(ComponentEnrollmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(authorized);
    }

    private sealed class StubTokenRedeemer(EnrollmentTokenValidationResult result) : IEnrollmentTokenRedeemer
    {
        public Task<EnrollmentTokenValidationResult> RedeemAsync(
            string token, ComponentType expectedComponentType, string expectedFqdn, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
