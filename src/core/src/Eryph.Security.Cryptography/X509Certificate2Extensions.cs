

using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Security;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Eryph.Security.Cryptography
{
    public static class X509CertificateExtensions
    {
        public static bool VerifyLocal(this X509Certificate cert)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy = new X509ChainPolicy
            {
                DisableCertificateDownloads = true,
                RevocationMode = X509RevocationMode.NoCheck,
            };
                
            var verified = chain.Build(new X509Certificate2(DotNetUtilities.ToX509Certificate(cert)));

            foreach (var t in chain.ChainElements)
            {
                t.Certificate.Dispose();
            }

            return verified;
        }
    }
}