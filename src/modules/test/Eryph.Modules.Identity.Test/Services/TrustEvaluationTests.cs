using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Rebus;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class TrustEvaluationTests
{
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";

    private static readonly CertificateGenerator Generator = new();

    [Fact]
    public void Accepts_a_server_cert_chaining_through_the_presented_intermediate_to_a_trusted_root()
    {
        var (root, leaf, presented) = BuildChain(ServerAuthOid);

        TrustEvaluation.IsTrustedServerCertificate(
            leaf, presented, SslPolicyErrors.RemoteCertificateChainErrors, [root])
            .Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_host_name_mismatch_even_when_the_chain_is_valid()
    {
        var (root, leaf, presented) = BuildChain(ServerAuthOid);

        TrustEvaluation.IsTrustedServerCertificate(
            leaf, presented, SslPolicyErrors.RemoteCertificateNameMismatch, [root])
            .Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_cert_that_does_not_chain_to_a_trusted_root()
    {
        var (_, leaf, presented) = BuildChain(ServerAuthOid);
        using var otherKey = RSA.Create(2048);
        var otherRoot = Generator.GenerateCaCertificate(
            new X500DistinguishedName("CN=other-root"), "other", otherKey, 3650, []);

        TrustEvaluation.IsTrustedServerCertificate(
            leaf, presented, SslPolicyErrors.RemoteCertificateChainErrors, [otherRoot])
            .Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_cert_without_the_serverAuth_eku()
    {
        // A client-auth leaf from the same hierarchy must not be accepted as a server certificate.
        var (root, leaf, presented) = BuildChain(ClientAuthOid);

        TrustEvaluation.IsTrustedServerCertificate(
            leaf, presented, SslPolicyErrors.RemoteCertificateChainErrors, [root])
            .Should().BeFalse();
    }

    private static (X509Certificate2 Root, X509Certificate2 Leaf, X509Chain Presented) BuildChain(string ekuOid)
    {
        using var rootKey = RSA.Create(2048);
        var root = Generator.GenerateCaCertificate(
            new X500DistinguishedName("CN=test-root"), "root", rootKey, 3650, []);

        using var intermediateKey = RSA.Create(2048);
        var intermediate = Generator.IssueIntermediateCaCertificate(
                new X500DistinguishedName("CN=test-intermediate"), "intermediate",
                intermediateKey, root, 1825, [])
            .CopyWithPrivateKey(intermediateKey);

        using var leafKey = RSA.Create(2048);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("server.eryph.test");
        var leaf = Generator.IssueCertificate(
            new X500DistinguishedName("CN=server.eryph.test"), "leaf", leafKey, intermediate, 90,
            [new X509EnhancedKeyUsageExtension([new Oid(ekuOid)], false), san.Build()]);

        // Simulate the chain the TLS stack presents (leaf + intermediate).
        var presented = new X509Chain();
        presented.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        presented.ChainPolicy.CustomTrustStore.Add(root);
        presented.ChainPolicy.ExtraStore.Add(intermediate);
        presented.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        presented.Build(leaf);

        return (root, leaf, presented);
    }
}
