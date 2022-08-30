using System;
using System.IO;
using FluentAssertions;
using Moq;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Xunit;

namespace Eryph.Security.Cryptography.Test;

public class CertificateGeneratorTests
{
    [Fact]
    public void GeneratesSelfSignedRootCertificate()
    {
        var testKey = GetTestPrivateKey();
        var rsaProvider = new Mock<IRSAProvider>();
        rsaProvider.Setup(x => x.CreateRSAKeyPair(2048)).Returns(testKey);


        var gen = new CertificateGenerator(rsaProvider.Object);
        var (cert, kp) = gen.GenerateSelfSignedCertificate(
            new X509Name("CN=test"), 10, 2048,
            cfg =>
            {
                cfg.AddExtension(
                    X509Extensions.BasicConstraints.Id,
                    true, new BasicConstraints(true));
            });

        kp.Should().Be(testKey);
        cert.SubjectDN.Should().Be(new X509Name("CN=test"));
        cert.IssuerDN.Should().Be(new X509Name("CN=test"));
        cert.NotBefore.Should().BeAfter(DateTime.Now.Subtract(new TimeSpan(1, 1,0,0)));
        cert.NotAfter.Should().BeBefore(DateTime.Now.Add(new TimeSpan(10, 0, 1, 0)));
        cert.SerialNumber.Should().NotBeNull();
 
    }
    
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
}