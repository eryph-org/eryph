using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Xunit;

namespace Eryph.Security.Cryptography.Test;

public class CertificateGeneratorIntermediateTests
{
    [Fact]
    public void IssueIntermediateCaCertificate_is_a_ca_that_chains_to_the_root_and_can_sign_leaves()
    {
        var generator = new CertificateGenerator();

        using var rootKey = RSA.Create(2048);
        var root = generator.GenerateCaCertificate(
            new X500DistinguishedName("CN=eryph-test-root"), "root", rootKey, 3650, []);

        using var intermediateKey = RSA.Create(2048);
        var intermediate = generator.IssueIntermediateCaCertificate(
            new X500DistinguishedName("CN=eryph-test-intermediate"), "intermediate",
            intermediateKey, root, 1825, []);

        // It is a CA (can sign), constrained to path length 0, and signed by the root.
        var basicConstraints = intermediate.Extensions.OfType<X509BasicConstraintsExtension>().Single();
        basicConstraints.CertificateAuthority.Should().BeTrue();
        basicConstraints.PathLengthConstraint.Should().Be(0);
        intermediate.Extensions.OfType<X509KeyUsageExtension>().Single()
            .KeyUsages.Should().HaveFlag(X509KeyUsageFlags.KeyCertSign);
        intermediate.IssuerName.RawData.Should().Equal(root.SubjectName.RawData);

        // The intermediate (with its private key) can issue a leaf that chains root -> intermediate -> leaf.
        using var intermediateWithKey = intermediate.CopyWithPrivateKey(intermediateKey);
        using var leafKey = RSA.Create(2048);
        var leaf = generator.IssueCertificate(
            new X500DistinguishedName("CN=leaf"), "leaf", leafKey, intermediateWithKey, 90, []);

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf).Should().BeTrue("the leaf must chain through the intermediate to the trusted root");
    }
}
