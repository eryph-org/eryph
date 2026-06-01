#nullable enable
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
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
    public void Enroll_issues_certificate_chaining_to_the_ca_with_a_server_derived_id()
    {
        var sut = CreateService(authorized: true);

        var result = sut.Enroll(new ComponentEnrollmentRequest
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
    public void Enroll_also_issues_a_server_certificate_when_a_server_key_is_supplied()
    {
        var sut = CreateService(authorized: true);

        var result = sut.Enroll(new ComponentEnrollmentRequest
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
    public void Enroll_omits_the_server_certificate_when_no_server_key_is_supplied()
    {
        var sut = CreateService(authorized: true);

        var result = sut.Enroll(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
        });

        result.ServerCertificate.Should().BeEmpty();
    }

    [Fact]
    public void Enroll_throws_when_the_policy_denies_the_request()
    {
        var sut = CreateService(authorized: false);

        var act = () => sut.Enroll(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.Identity,
            Fqdn = "id.eryph.local",
            PublicKey = NewPublicKey(),
        });

        act.Should().Throw<ComponentEnrollmentException>();
    }

    [Fact]
    public void Enroll_throws_on_an_invalid_public_key()
    {
        var sut = CreateService(authorized: true);

        var act = () => sut.Enroll(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.ComputeApi,
            Fqdn = "api.eryph.local",
            PublicKey = [1, 2, 3],
        });

        act.Should().Throw<ComponentEnrollmentException>();
    }

    [Fact]
    public void TokenEnrollmentPolicy_authorizes_a_valid_token_for_the_bound_type()
    {
        var policy = new TokenEnrollmentPolicy(
            new StubTokenService(EnrollmentTokenValidationResult.Valid(ComponentType.VMHostAgent)),
            NullLogger<TokenEnrollmentPolicy>.Instance);

        policy.IsAuthorized(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Token = "token",
        }).Should().BeTrue();
    }

    [Fact]
    public void TokenEnrollmentPolicy_denies_on_type_mismatch_or_invalid_token()
    {
        var typeMismatch = new TokenEnrollmentPolicy(
            new StubTokenService(EnrollmentTokenValidationResult.Valid(ComponentType.Controller)),
            NullLogger<TokenEnrollmentPolicy>.Instance);
        typeMismatch.IsAuthorized(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Token = "token",
        }).Should().BeFalse();

        var invalid = new TokenEnrollmentPolicy(
            new StubTokenService(EnrollmentTokenValidationResult.Invalid),
            NullLogger<TokenEnrollmentPolicy>.Instance);
        invalid.IsAuthorized(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.VMHostAgent,
            Token = "token",
        }).Should().BeFalse();
    }

    private sealed class StubPolicy(bool authorized) : IComponentEnrollmentPolicy
    {
        public bool IsAuthorized(ComponentEnrollmentRequest request) => authorized;
    }

    private sealed class StubTokenService(EnrollmentTokenValidationResult result) : IEnrollmentTokenService
    {
        public string Mint(ComponentType componentType, TimeSpan validFor) => "token";
        public EnrollmentTokenValidationResult Redeem(string token) => result;
    }
}
