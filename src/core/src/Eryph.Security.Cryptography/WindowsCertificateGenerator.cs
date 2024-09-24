using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

[SupportedOSPlatform("windows")]
public class WindowsCertificateGenerator : CertificateGenerator
{
    public override X509Certificate2 GenerateSelfSignedCertificate(
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA keyPair,
        int validDays,
        IReadOnlyList<X509Extension> extensions)
    {
        var certificate = base.GenerateSelfSignedCertificate(
            subjectName, friendlyName, keyPair, validDays, extensions);
        
        certificate.FriendlyName = friendlyName;
        return certificate;
    }
}
