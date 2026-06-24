using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Xunit;

namespace Eryph.Security.Cryptography.Test;

public class CertificateGeneratorTests
{
    [Fact]
    public void GenerateCaCertificate_ProducesUsableCaCertificate()
    {
        using var caKey = RSA.Create(2048);
        var generator = new CertificateGenerator();

        using var ca = generator.GenerateCaCertificate(
            new X500DistinguishedName("CN=Test CA"), "Test CA", caKey, 365, []);

        ca.HasPrivateKey.Should().BeTrue();
        ca.Subject.Should().Be("CN=Test CA");
        ca.Issuer.Should().Be("CN=Test CA");

        var basicConstraints = ca.Extensions.OfType<X509BasicConstraintsExtension>().Single();
        basicConstraints.CertificateAuthority.Should().BeTrue();
        basicConstraints.Critical.Should().BeTrue();

        var keyUsage = ca.Extensions.OfType<X509KeyUsageExtension>().Single();
        keyUsage.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.KeyCertSign);
        keyUsage.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.CrlSign);
    }

    [Fact]
    public void IssueCertificate_IsSignedByCaAndChainsToIt()
    {
        using var caKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);
        var generator = new CertificateGenerator();

        using var ca = generator.GenerateCaCertificate(
            new X500DistinguishedName("CN=Test CA"), "Test CA", caKey, 365, []);

        using var leaf = generator.IssueCertificate(
            new X500DistinguishedName("CN=component-1"),
            "component-1",
            leafKey,
            ca,
            90,
            [
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid(Oids.EnhancedKeyUsage.ClientAuthentication) }, false),
            ]);

        leaf.Subject.Should().Be("CN=component-1");
        leaf.Issuer.Should().Be("CN=Test CA");
        // The issued certificate carries no private key; the caller attaches it.
        leaf.HasPrivateKey.Should().BeFalse();

        leaf.Extensions.OfType<X509BasicConstraintsExtension>().Single()
            .CertificateAuthority.Should().BeFalse();
        leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single()
            .EnhancedKeyUsages[0].Value.Should().Be(Oids.EnhancedKeyUsage.ClientAuthentication);

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var built = chain.Build(leaf);
        built.Should().BeTrue(
            string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation)));
    }

    [Fact]
    public void IssueCertificate_DoesNotOutliveIssuer()
    {
        using var caKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);
        var generator = new CertificateGenerator();

        // CA valid for only 10 days; leaf requested for 90 must be capped.
        using var ca = generator.GenerateCaCertificate(
            new X500DistinguishedName("CN=Short CA"), "Short CA", caKey, 10, []);

        using var leaf = generator.IssueCertificate(
            new X500DistinguishedName("CN=component-2"), "component-2", leafKey, ca, 90, []);

        leaf.NotAfter.Should().BeOnOrBefore(ca.NotAfter);
    }
}
