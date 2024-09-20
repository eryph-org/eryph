using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Xunit;

namespace Eryph.Security.Cryptography.Test;

public class CertificateGeneratorTests
{
    [Fact]
    public void GenerateSelfSignedCertificate_GeneratesCorrectCertificate()
    {
        using var rsa = RSA.Create(2048);
        var expectedSubjectKeyId = new X509SubjectKeyIdentifierExtension(new PublicKey(rsa), false)
            .SubjectKeyIdentifier;

        var subjectName = new X500DistinguishedName("CN=test");

        var generator = new CertificateGenerator();
        using var certificate = generator.GenerateSelfSignedCertificate(
            subjectName,
            "test certificate",
            rsa,
            10,
            [
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true),
            ]);

        certificate.FriendlyName.Should().Be("test certificate");
        certificate.HasPrivateKey.Should().BeTrue();
        certificate.Subject.Should().Be("CN=test");
        certificate.Issuer.Should().Be("CN=test");
        certificate.NotBefore.Should().BeCloseTo(DateTime.Now.AddDays(-1), TimeSpan.FromMinutes(10));
        certificate.NotAfter.Should().BeCloseTo(DateTime.Now.AddDays(10), TimeSpan.FromMinutes(10));
        certificate.SerialNumber.Should().NotBeEmpty();

        certificate.Extensions.Should().SatisfyRespectively(
            extension =>
            {
                var basicConstraints = extension.Should().BeOfType<X509BasicConstraintsExtension>().Subject;
                basicConstraints.CertificateAuthority.Should().BeFalse();
                basicConstraints.Critical.Should().BeTrue();
            },
            extension =>
            {
                var subjectKeyIdentifier = extension.Should().BeOfType<X509SubjectKeyIdentifierExtension>().Subject;
                subjectKeyIdentifier.SubjectKeyIdentifier.Should().Be(expectedSubjectKeyId);
                subjectKeyIdentifier.Critical.Should().BeFalse();
            },
            extension =>
            {
                var authorityKeyIdentifier = extension.Should().BeOfType<X509AuthorityKeyIdentifierExtension>().Subject;
                authorityKeyIdentifier.Critical.Should().BeFalse();
            },
            extension =>
            {
                var keyUsage = extension.Should().BeOfType<X509KeyUsageExtension>().Subject;
                keyUsage.KeyUsages.Should().Be(X509KeyUsageFlags.DigitalSignature);
                keyUsage.Critical.Should().BeTrue();
            });
    }
}
