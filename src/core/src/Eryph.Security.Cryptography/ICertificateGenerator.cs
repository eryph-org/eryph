using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = System.Security.Cryptography.X509Certificates.X509Extension;

namespace Eryph.Security.Cryptography
{
    public interface ICertificateGenerator
    {
        /// <summary>
        ///     Create a BouncyCastle AsymmetricCipherKeyPair and associated
        ///     X509Certificate
        ///     <remarks>
        ///         Based on:
        ///         http://stackoverflow.com/questions/3770233/is-it-possible-to-programmatically-generate-an-x509-certificate-using-only-c
        ///         http://web.archive.org/web/20100504192226/http://www.fkollmann.de/v2/post/Creating-certificates-using-BouncyCastle.aspx
        ///         requires http://www.bouncycastle.org/csharp/
        ///     </remarks>

        /// </summary>
        (X509Certificate Certificate, AsymmetricCipherKeyPair KeyPair) GenerateSelfSignedCertificate(
            X509Name subjectName,
            int validDays,
            int keyLength,
            Action<X509V3CertificateGenerator>? configureGenerator = null);

        X509Certificate2 GenerateSelfSignedCertificate(
            X500DistinguishedName subjectName,
            RSA keyPair,
            int validDays,
            IReadOnlyList<X509Extension> extensions);
    }
}