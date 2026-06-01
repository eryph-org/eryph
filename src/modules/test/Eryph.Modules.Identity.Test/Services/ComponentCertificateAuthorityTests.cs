#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class ComponentCertificateAuthorityTests
{
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    private static ComponentCertificateAuthority CreateCa() =>
        new(new InMemoryCertificateStore(), new CertificateGenerator(), new InMemoryKeyService());

    private static bool ChainsToTrustedRoot(IssuedCertificate issued, ComponentCertificateAuthority ca)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        foreach (var root in ca.GetTrustedCaCertificates())
            chain.ChainPolicy.CustomTrustStore.Add(root);
        foreach (var intermediate in issued.IssuingChain)
            chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(issued.Leaf);
    }

    private static string[] EnhancedKeyUsages(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Single().EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value!).ToArray();

    [Fact]
    public void GetTrustedCaCertificates_returns_a_root_ca()
    {
        var bundle = CreateCa().GetTrustedCaCertificates();

        bundle.Should().ContainSingle().Which.Extensions
            .OfType<X509BasicConstraintsExtension>().Should()
            .ContainSingle().Which.CertificateAuthority.Should().BeTrue();
    }

    [Fact]
    public void IssueComponentCertificate_issues_a_client_leaf_chaining_through_the_intermediate_to_the_root()
    {
        var sut = CreateCa();
        using var key = RSA.Create(2048);
        const string componentId = "11111111-2222-3333-4444-555555555555";
        const string fqdn = "agent1.eryph.local";

        var issued = sut.IssueComponentCertificate(componentId, fqdn, key);

        issued.IssuingChain.Should().NotBeEmpty("the leaf must be presentable with its intermediate");
        ChainsToTrustedRoot(issued, sut).Should().BeTrue();
        EnhancedKeyUsages(issued.Leaf).Should().Contain(ClientAuthOid);

        var san = issued.Leaf.Extensions.Single(e => e.Oid?.Value == "2.5.29.17").Format(false);
        san.Should().Contain(fqdn);
        san.Should().Contain(componentId);

        (issued.Leaf.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays.Should().BeInRange(85, 95);
    }

    [Fact]
    public void IssueServerCertificate_issues_a_server_leaf_chaining_to_the_root()
    {
        var sut = CreateCa();
        using var key = RSA.Create(2048);

        var issued = sut.IssueServerCertificate(["identity.eryph.local"], key);

        ChainsToTrustedRoot(issued, sut).Should().BeTrue();
        EnhancedKeyUsages(issued.Leaf).Should().Contain(ServerAuthOid);
        issued.Leaf.Extensions.Single(e => e.Oid?.Value == "2.5.29.17").Format(false)
            .Should().Contain("identity.eryph.local");
    }

    [Fact]
    public void IssueServerCertificate_covers_every_requested_dns_name()
    {
        var sut = CreateCa();
        using var key = RSA.Create(2048);

        var issued = sut.IssueServerCertificate(["api.eryph.local", "api", "compute.eryph.local"], key);

        var san = issued.Leaf.Extensions.Single(e => e.Oid?.Value == "2.5.29.17").Format(false);
        san.Should().Contain("api.eryph.local").And.Contain("api").And.Contain("compute.eryph.local");
    }

    [Fact]
    public void IssueServerCertificate_rejects_an_empty_name_list()
    {
        var sut = CreateCa();
        using var key = RSA.Create(2048);

        var act = () => sut.IssueServerCertificate([], key);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Client_and_server_intermediates_are_distinct()
    {
        var sut = CreateCa();
        using var clientKey = RSA.Create(2048);
        using var serverKey = RSA.Create(2048);

        var client = sut.IssueComponentCertificate("id", "agent.eryph.local", clientKey);
        var server = sut.IssueServerCertificate(["api.eryph.local"], serverKey);

        client.IssuingChain[0].Thumbprint.Should().NotBe(server.IssuingChain[0].Thumbprint);
    }

    [Fact]
    public void GetTrustedCaCertificates_keeps_multiple_root_generations()
    {
        var store = new InMemoryCertificateStore();
        var sut = new ComponentCertificateAuthority(store, new CertificateGenerator(), new InMemoryKeyService());

        sut.GetTrustedCaCertificates(); // establishes the first root

        // A second, still-valid root generation under the same subject (as a future rollover adds).
        var subject = new X500DistinguishedNameBuilder();
        subject.AddOrganizationName("eryph");
        subject.AddOrganizationalUnitName("component-ca");
        subject.AddCommonName("eryph-component-root-ca");
        using var secondKey = RSA.Create(2048);
        store.AddToMyStore(new CertificateGenerator().GenerateCaCertificate(
            subject.Build(), "eryph component root CA", secondKey, 3650, []));

        sut.GetTrustedCaCertificates().Should().HaveCount(2);
    }
}
