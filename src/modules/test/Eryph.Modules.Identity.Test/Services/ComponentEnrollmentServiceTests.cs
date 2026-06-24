#nullable enable
using System;
using System.Collections.Generic;
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

    private static ComponentEnrollmentService CreateService(
        bool authorized, IComponentBrokerProvisioner? brokerProvisioner = null) =>
        new(CreateCa(), new StubPolicy(authorized),
            brokerProvisioner is null ? [] : [brokerProvisioner],
            NullLogger<ComponentEnrollmentService>.Instance);

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
        // X509Chain does not own certificates added to its policy stores, so track and dispose them.
        var loaded = new List<X509Certificate2>();
        try
        {
            foreach (var rootDer in result.CaTrustBundle)
            {
                var root = X509CertificateLoader.LoadCertificate(rootDer);
                loaded.Add(root);
                chain.ChainPolicy.CustomTrustStore.Add(root);
            }
            foreach (var intermediateDer in result.IssuingChain)
            {
                var intermediate = X509CertificateLoader.LoadCertificate(intermediateDer);
                loaded.Add(intermediate);
                chain.ChainPolicy.ExtraStore.Add(intermediate);
            }
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.Build(leaf).Should().BeTrue(
                "the issued leaf must chain through the intermediate to a root in the returned bundle");
        }
        finally
        {
            foreach (var certificate in loaded)
                certificate.Dispose();
        }
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
            return new ComponentEnrollmentService(ca, policy, [], NullLogger<ComponentEnrollmentService>.Instance);
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
            return new ComponentEnrollmentService(ca, policy, [], NullLogger<ComponentEnrollmentService>.Instance);
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

    [Fact]
    public async Task Renew_issues_a_fresh_certificate_for_the_identity_in_the_presented_certificate()
    {
        // One CA across both calls so the enrolled leaf chains to the same root the renewal validates against.
        var ca = CreateCa();
        var sut = new ComponentEnrollmentService(ca, new StubPolicy(true), [], NullLogger<ComponentEnrollmentService>.Instance);

        var enrolled = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        // Renewal authenticates with the (CA-issued) leaf, presented without a token.
        using var leaf = X509CertificateLoader.LoadCertificate(enrolled.Certificate);
        var renewed = await sut.RenewAsync(leaf, new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        renewed.ComponentId.Should().Be(enrolled.ComponentId);
        renewed.Certificate.Should().NotBeEmpty();
        renewed.IssuingChain.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Renew_takes_the_identity_from_the_certificate_not_the_request()
    {
        var ca = CreateCa();
        var sut = new ComponentEnrollmentService(ca, new StubPolicy(true), [], NullLogger<ComponentEnrollmentService>.Instance);

        var enrolled = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        // The request claims a different type and host; the renewed identity must still be the one
        // bound in the presented certificate, so a component cannot renew into another identity.
        using var leaf = X509CertificateLoader.LoadCertificate(enrolled.Certificate);
        var renewed = await sut.RenewAsync(leaf, new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "attacker.eryph.local",
            PublicKey = NewPublicKey(),
        });

        renewed.ComponentId.Should().Be(
            ComponentIdentity.DeriveComponentId(ComponentType.VMHostAgent, "agent1.eryph.local"));
    }

    [Fact]
    public async Task Renew_rejects_a_certificate_not_issued_by_the_component_ca()
    {
        var sut = CreateService(authorized: true);

        // A self-signed certificate does not chain to the component root, so renewal must refuse it
        // even though it carries a plausible subject — only a CA-issued component cert may renew.
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=agent1.eryph.local", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var foreign = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var act = () => sut.RenewAsync(foreign, new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        await act.Should().ThrowAsync<ComponentEnrollmentException>();
    }

    [Fact]
    public async Task Enroll_provisions_the_broker_user_for_the_issued_identity()
    {
        var broker = new RecordingBrokerProvisioner();
        var sut = CreateService(authorized: true, brokerProvisioner: broker);

        var result = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        // The broker user must be ensured for the same id the certificate was issued for, before the
        // component connects to the bus.
        broker.Ensured.Should().ContainSingle().Which.Should().Be(result.ComponentId);
        broker.Removed.Should().BeEmpty();
    }

    [Fact]
    public async Task Renew_provisions_the_broker_user()
    {
        var ca = CreateCa();
        var broker = new RecordingBrokerProvisioner();
        var sut = new ComponentEnrollmentService(
            ca, new StubPolicy(true), [broker], NullLogger<ComponentEnrollmentService>.Instance);

        var enrolled = await sut.EnrollAsync(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        using var leaf = X509CertificateLoader.LoadCertificate(enrolled.Certificate);
        await sut.RenewAsync(leaf, new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        }, CancellationToken.None);

        // Ensured once at enrollment and again (idempotently) at renewal.
        broker.Ensured.Should().Equal(enrolled.ComponentId, enrolled.ComponentId);
    }

    private sealed class RecordingBrokerProvisioner : IComponentBrokerProvisioner
    {
        public List<Guid> Ensured { get; } = [];
        public List<Guid> Removed { get; } = [];

        public Task EnsureComponentAsync(Guid componentId, CancellationToken cancellationToken = default)
        {
            Ensured.Add(componentId);
            return Task.CompletedTask;
        }

        public Task RemoveComponentAsync(Guid componentId, CancellationToken cancellationToken = default)
        {
            Removed.Add(componentId);
            return Task.CompletedTask;
        }
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
