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
            Credential = "anything",
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
    public void Enroll_throws_when_the_policy_denies_the_request()
    {
        var sut = CreateService(authorized: false);

        var act = () => sut.Enroll(new ComponentEnrollmentRequest
        {
            ComponentType = ComponentType.Identity,
            Fqdn = "id.eryph.local",
            PublicKey = NewPublicKey(),
            Credential = "wrong",
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
            Credential = "anything",
        });

        act.Should().Throw<ComponentEnrollmentException>();
    }

    [Theory]
    [InlineData("s3cret", "s3cret", true)]
    [InlineData("s3cret", "wrong", false)]
    [InlineData("", "anything", false)]
    public void SharedSecretEnrollmentPolicy_authorizes_only_on_a_matching_secret(
        string configured, string provided, bool expected)
    {
        var policy = new SharedSecretEnrollmentPolicy(
            configured, NullLogger<SharedSecretEnrollmentPolicy>.Instance);

        policy.IsAuthorized(new ComponentEnrollmentRequest { Credential = provided })
            .Should().Be(expected);
    }

    private sealed class StubPolicy(bool authorized) : IComponentEnrollmentPolicy
    {
        public bool IsAuthorized(ComponentEnrollmentRequest request) => authorized;
    }
}
