using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

public class CertificateGenerator : ICertificateGenerator
{
    public X509Certificate2 GenerateSelfSignedCertificate(
        X500DistinguishedName subjectName,
        RSA keyPair,
        int validDays,
        IReadOnlyList<X509Extension> extensions)
    {
        var request = new CertificateRequest(subjectName, keyPair, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));

        var publicKey = new PublicKey(keyPair);
        var subjectKeyIdentifier = new X509SubjectKeyIdentifierExtension(publicKey, false);
        var authorityKeyIdentifier = X509AuthorityKeyIdentifierExtension
            .CreateFromSubjectKeyIdentifier(subjectKeyIdentifier);
        request.CertificateExtensions.Add(subjectKeyIdentifier);
        request.CertificateExtensions.Add(authorityKeyIdentifier);

        foreach (var extension in extensions)
        {
            request.CertificateExtensions.Add(extension);
        }

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1)),
            DateTimeOffset.UtcNow.AddDays(validDays));

        return certificate;
    }
}
