using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

public class CertificateGenerator : ICertificateGenerator
{
    public virtual X509Certificate2 GenerateSelfSignedCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
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

    public virtual X509Certificate2 GenerateCaCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA keyPair,
        int validDays,
        IReadOnlyList<X509Extension> extensions)
    {
        var request = new CertificateRequest(subjectName, keyPair, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

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

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1)),
            DateTimeOffset.UtcNow.AddDays(validDays));
    }

    public virtual X509Certificate2 IssueCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA subjectKey,
        X509Certificate2 issuerCertificate,
        int validDays,
        IReadOnlyList<X509Extension> extensions)
    {
        var request = new CertificateRequest(subjectName, subjectKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(new PublicKey(subjectKey), false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuerCertificate, true, false));

        foreach (var extension in extensions)
        {
            request.CertificateExtensions.Add(extension);
        }

        // Random positive 128-bit serial number, unique per issued certificate.
        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;

        var notBefore = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1));
        var notAfter = DateTimeOffset.UtcNow.AddDays(validDays);
        // The signed certificate must not outlive its issuer. Compare in UTC:
        // X509Certificate2.NotAfter is a local-kind DateTime, so a naive comparison
        // against the UTC notAfter would be off by the host's UTC offset.
        var issuerNotAfter = issuerCertificate.NotAfter.ToUniversalTime();
        if (notAfter > issuerNotAfter)
            notAfter = issuerNotAfter;

        return request.Create(issuerCertificate, notBefore, notAfter, serialNumber);
    }

    public virtual X509Certificate2 IssueIntermediateCaCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA subjectKey,
        X509Certificate2 issuerCertificate,
        int validDays,
        IReadOnlyList<X509Extension> extensions)
    {
        var request = new CertificateRequest(subjectName, subjectKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        // CA=true with path length 0: may sign leaf certificates but not further CA certificates.
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(new PublicKey(subjectKey), false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuerCertificate, true, false));

        foreach (var extension in extensions)
        {
            request.CertificateExtensions.Add(extension);
        }

        // Random positive 128-bit serial number, unique per issued certificate.
        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;

        var notBefore = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1));
        var notAfter = DateTimeOffset.UtcNow.AddDays(validDays);
        // An intermediate must not outlive the root that signed it (see IssueCertificate).
        var issuerNotAfter = issuerCertificate.NotAfter.ToUniversalTime();
        if (notAfter > issuerNotAfter)
            notAfter = issuerNotAfter;

        return request.Create(issuerCertificate, notBefore, notAfter, serialNumber);
    }
}
