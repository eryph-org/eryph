using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

public interface ICertificateGenerator
{
    X509Certificate2 GenerateSelfSignedCertificate(
        X500DistinguishedName subjectName,
        RSA keyPair,
        int validDays,
        IReadOnlyList<X509Extension> extensions);
}
