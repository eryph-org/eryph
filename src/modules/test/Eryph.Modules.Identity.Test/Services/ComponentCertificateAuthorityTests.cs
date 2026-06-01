#nullable enable
using System;
using System.IO;
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

    [Fact]
    public void File_backed_ca_persists_keys_and_reloads_across_instances()
    {
        // The cross-platform (Linux default) backend: keys + certs live in a directory, not CNG/the
        // Windows store. A fresh CA instance over the same directory must reuse the persisted root
        // (with its private key), so it keeps one root and keeps issuing certs that chain to it.
        var dir = Path.Combine(Path.GetTempPath(), "eryph-ca-file-" + Guid.NewGuid().ToString("N"));
        try
        {
            ComponentCertificateAuthority NewCa() => new(
                new FileCertificateStoreService(dir),
                new CertificateGenerator(),
                new FileCertificateKeyService(Path.Combine(dir, "keys")));

            using var key1 = RSA.Create(2048);
            using var issued1 = NewCa().IssueComponentCertificate("id-1", "agent1.eryph.local", key1);

            var reloaded = NewCa();
            var roots = reloaded.GetTrustedCaCertificates();
            roots.Should().ContainSingle("a fresh instance reuses the persisted root, not a new one");
            roots[0].HasPrivateKey.Should().BeTrue("the root key persisted to disk and reloaded");
            foreach (var root in roots)
                root.Dispose();

            using var key2 = RSA.Create(2048);
            using var issued2 = reloaded.IssueComponentCertificate("id-2", "agent2.eryph.local", key2);
            ChainsToTrustedRoot(issued2, reloaded).Should().BeTrue("issuance still chains to the persisted root");
            // Same client intermediate across instances => its private key persisted and reloaded
            // (a key that failed to reload would force the CA to regenerate a new intermediate).
            issued2.IssuingChain[0].Thumbprint.Should().Be(issued1.IssuingChain[0].Thumbprint,
                "the persisted client intermediate is reused, not regenerated");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private static bool ChainsToTrustedRoot(IssuedCertificate issued, ComponentCertificateAuthority ca)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        // GetTrustedCaCertificates returns caller-owned handles; dispose them once the chain is built.
        var roots = ca.GetTrustedCaCertificates();
        try
        {
            foreach (var root in roots)
                chain.ChainPolicy.CustomTrustStore.Add(root);
            foreach (var intermediate in issued.IssuingChain)
                chain.ChainPolicy.ExtraStore.Add(intermediate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(issued.Leaf);
        }
        finally
        {
            foreach (var root in roots)
                root.Dispose();
        }
    }

    private static string[] EnhancedKeyUsages(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Single().EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value!).ToArray();

    [Fact]
    public void Regenerating_the_root_reissues_intermediates_so_new_leaves_chain_to_the_recovered_root()
    {
        var store = new InMemoryCertificateStore();
        var sut = new ComponentCertificateAuthority(store, new CertificateGenerator(), new InMemoryKeyService());

        using var key1 = RSA.Create(2048);
        using var issued1 = sut.IssueComponentCertificate("id-1", "agent1.eryph.local", key1);
        ChainsToTrustedRoot(issued1, sut).Should().BeTrue();

        // Simulate loss of the root certificate (corrupted/removed): the CA must regenerate it. The old
        // client intermediate is still in the store and chains to the now-gone root.
        var rootSubject = new X500DistinguishedNameBuilder();
        rootSubject.AddOrganizationName("eryph");
        rootSubject.AddOrganizationalUnitName("component-ca");
        rootSubject.AddCommonName("eryph-component-root-ca");
        store.RemoveFromMyStore(rootSubject.Build());

        using var key2 = RSA.Create(2048);
        using var issued2 = sut.IssueComponentCertificate("id-2", "agent2.eryph.local", key2);

        // The new leaf must chain to the regenerated (now only-trusted) root — i.e. the orphaned
        // intermediate was re-issued from the new root, not reused. Without that, the chain would build
        // to the removed root and fail validation against GetTrustedCaCertificates.
        ChainsToTrustedRoot(issued2, sut).Should().BeTrue(
            "after a root regeneration the intermediate is re-issued so leaves chain to the new root");
        issued2.IssuingChain[0].Thumbprint.Should().NotBe(issued1.IssuingChain[0].Thumbprint,
            "the orphaned intermediate must be replaced, not reused");
    }

    [Fact]
    public void GetTrustedCaCertificates_returns_a_root_ca()
    {
        var bundle = CreateCa().GetTrustedCaCertificates();
        try
        {
            bundle.Should().ContainSingle().Which.Extensions
                .OfType<X509BasicConstraintsExtension>().Should()
                .ContainSingle().Which.CertificateAuthority.Should().BeTrue();
        }
        finally
        {
            foreach (var certificate in bundle)
                certificate.Dispose();
        }
    }

    [Fact]
    public void IssueComponentCertificate_issues_a_client_leaf_chaining_through_the_intermediate_to_the_root()
    {
        var sut = CreateCa();
        using var key = RSA.Create(2048);
        const string componentId = "11111111-2222-3333-4444-555555555555";
        const string fqdn = "agent1.eryph.local";

        using var issued = sut.IssueComponentCertificate(componentId, fqdn, key);

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

        using var issued = sut.IssueServerCertificate(["identity.eryph.local"], key);

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

        using var issued = sut.IssueServerCertificate(["api.eryph.local", "api", "compute.eryph.local"], key);

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

        using var client = sut.IssueComponentCertificate("id", "agent.eryph.local", clientKey);
        using var server = sut.IssueServerCertificate(["api.eryph.local"], serverKey);

        client.IssuingChain[0].Thumbprint.Should().NotBe(server.IssuingChain[0].Thumbprint);
    }

    [Fact]
    public void GetTrustedCaCertificates_keeps_multiple_root_generations()
    {
        var store = new InMemoryCertificateStore();
        var sut = new ComponentCertificateAuthority(store, new CertificateGenerator(), new InMemoryKeyService());

        foreach (var root in sut.GetTrustedCaCertificates()) // establishes the first root
            root.Dispose();

        // A second, still-valid root generation under the same subject (as a future rollover adds).
        var subject = new X500DistinguishedNameBuilder();
        subject.AddOrganizationName("eryph");
        subject.AddOrganizationalUnitName("component-ca");
        subject.AddCommonName("eryph-component-root-ca");
        using var secondKey = RSA.Create(2048);
        using var secondRoot = new CertificateGenerator().GenerateCaCertificate(
            subject.Build(), "eryph component root CA", secondKey, 3650, []);
        store.AddToMyStore(secondRoot);

        var bundle = sut.GetTrustedCaCertificates();
        try
        {
            bundle.Should().HaveCount(2);
        }
        finally
        {
            foreach (var certificate in bundle)
                certificate.Dispose();
        }
    }
}
