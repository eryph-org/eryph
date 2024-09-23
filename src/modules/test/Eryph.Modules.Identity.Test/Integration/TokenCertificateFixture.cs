using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;
using Xunit;

namespace Eryph.Modules.Identity.Test.Integration;

public sealed class TokenCertificateFixture : IDisposable
{
    public TokenCertificateFixture()
    {
        var certificateGenerator = new CertificateGenerator();

        using var encryptionKey = RSA.Create(2048);
        EncryptionCertificate = certificateGenerator.GenerateSelfSignedCertificate(
            new X500DistinguishedName("CN=test-token-encryption"),
            "test token encryption",
            encryptionKey,
            10,
            [new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true)]);

        using var signingKey = RSA.Create(2048);
        SigningCertificate = certificateGenerator.GenerateSelfSignedCertificate(
            new X500DistinguishedName("CN=test-token-signing"),
            "test token signing",
            signingKey,
            10,
            [new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true)]);
    }

    public X509Certificate2 EncryptionCertificate { get; }

    public X509Certificate2 SigningCertificate { get; }

    public void Dispose()
    {
        EncryptionCertificate.Dispose();
        SigningCertificate.Dispose();
    }
}

[CollectionDefinition("Token certificate collection")]
public class TokenCertificateCollection : ICollectionFixture<TokenCertificateFixture>;
