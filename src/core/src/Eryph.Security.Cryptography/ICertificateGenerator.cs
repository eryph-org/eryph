using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

public interface ICertificateGenerator
{
    X509Certificate2 GenerateSelfSignedCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA keyPair,
        int validDays,
        IReadOnlyList<X509Extension> extensions);

    /// <summary>
    /// Generates a self-signed certificate that can act as a certificate
    /// authority (basic constraints CA=true, key usage KeyCertSign|CrlSign).
    /// Use the returned certificate as the <c>issuerCertificate</c> for
    /// <see cref="IssueCertificate"/>.
    /// </summary>
    X509Certificate2 GenerateCaCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA keyPair,
        int validDays,
        IReadOnlyList<X509Extension> extensions);

    /// <summary>
    /// Issues a leaf certificate for <paramref name="subjectKey"/>, signed by
    /// <paramref name="issuerCertificate"/> (which must hold a private key).
    /// The returned certificate does not contain a private key; the caller
    /// associates the subject's private key separately.
    /// </summary>
    X509Certificate2 IssueCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA subjectKey,
        X509Certificate2 issuerCertificate,
        int validDays,
        IReadOnlyList<X509Extension> extensions);

    /// <summary>
    /// Issues an intermediate CA certificate (basic constraints CA=true with path length 0, so it
    /// may sign leaf certificates but no further CAs; key usage KeyCertSign|CrlSign) for
    /// <paramref name="subjectKey"/>, signed by <paramref name="issuerCertificate"/> (the root,
    /// which must hold a private key). The returned certificate does not contain a private key;
    /// the caller associates the intermediate's private key separately (e.g. via
    /// <see cref="X509Certificate2.CopyWithPrivateKey"/>) before using it to sign leaves.
    /// </summary>
    X509Certificate2 IssueIntermediateCaCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA subjectKey,
        X509Certificate2 issuerCertificate,
        int validDays,
        IReadOnlyList<X509Extension> extensions);
}
