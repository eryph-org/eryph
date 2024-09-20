using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Moq;
using Xunit;

namespace Eryph.Security.Cryptography.Test;

public class CertificateGeneratorTests
{
    
    [Fact]
    public void GenerateSelfSignedCertificate_GeneratesCorrectCertificate()
    {
        var sw = Stopwatch.StartNew();
        using var rsa = RSA.Create(2048);
        var time = sw.ElapsedTicks;


        var subjectName = new X500DistinguishedName("CN=test");

        var generator = new CertificateGenerator();
        using var certificate = generator.GenerateSelfSignedCertificate(
            subjectName,
            "test certificate",
            rsa,
            10,
            []);

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

            });

        /*
        cert.SubjectDN.Should().Be(new X509Name("CN=test"));
        cert.IssuerDN.Should().Be(new X509Name("CN=test"));
        cert.NotBefore.Should().BeAfter(DateTime.Now.Subtract(new TimeSpan(1, 1,0,0)));
        cert.NotAfter.Should().BeBefore(DateTime.Now.Add(new TimeSpan(10, 0, 1, 0)));
        cert.SerialNumber.Should().NotBeNull();
        */
    }
    
    /*
    private static AsymmetricCipherKeyPair GetTestPrivateKey()
    {
        
        const string keyString = @"-----BEGIN RSA PRIVATE KEY-----
MIIBOQIBAAJBAIF1DAqrNpUaH+dUf0l4M1AYkVqsDXW/k/+jZdWEec4FIAWBT16i
AishEvQT/SS238dzGtVEsCZyFWvp3m3Oip0CAwEAAQJAEh7lpBqqJb3F6HYR6SFL
oXMG6Ze6vJgn6bkf+H62JAmtiyiqzmfLgTkugZxiGei9zglsMAqQCMJk7IGe9aCs
iwIhAMIvvlBefrJ2vf5F0EjxXlhpDbU8XASyGeraQVO+yQvjAiEAqqqDxmPmluEr
9YdoZR6S5zSraG5J6GEXR1RRlgxy138CIDgD/7k9WPzwJeRojSnNfrKwM0UZkU3F
dpZ5uSiIO4STAiB/BWQIX0g7GaIHHt3TDPtXO3srwZIec0zJGPeUDvXWbwIgXKjg
9yLTnafGGFSjTW2RLbOhJEp5p5gt7PnityO8kXI=
-----END RSA PRIVATE KEY-----";

        using var reader = new StringReader(keyString);
        return (AsymmetricCipherKeyPair) new PemReader(reader).ReadObject();
    }
    */
}