#nullable enable
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Rebus
{
    /// <summary>
    /// Shared validation for custom-root mTLS server certificates (used by the RabbitMQ transport
    /// and the component enrollment HTTP client). A server certificate is trusted only when the
    /// host name matches, it chains to one of the supplied trusted roots — building through the
    /// intermediates the peer presented in the handshake — and it carries the serverAuth EKU.
    /// </summary>
    public static class TrustEvaluation
    {
        private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

        public static bool IsTrustedServerCertificate(
            X509Certificate? certificate,
            X509Chain? presentedChain,
            SslPolicyErrors errors,
            X509Certificate2Collection trustedRoots)
        {
            if (certificate is null || trustedRoots is null || trustedRoots.Count == 0)
                return false;

            // A host-name mismatch or a missing certificate is never acceptable. Only a chain error
            // (the machine store doesn't know our private root) may be tolerated — and only because
            // we re-validate against the custom root below.
            if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
                return false;

            using var serverCertificate = X509CertificateLoader.LoadCertificate(
                certificate.Export(X509ContentType.Cert));

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);

            // Include the intermediates the peer presented so a leaf -> intermediate -> root chain
            // can be built (the fresh chain has no other source for intermediates).
            if (presentedChain is not null)
            {
                foreach (var element in presentedChain.ChainElements)
                    chain.ChainPolicy.ExtraStore.Add(element.Certificate);
            }

            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            return chain.Build(serverCertificate) && HasServerAuthEku(serverCertificate);
        }

        private static bool HasServerAuthEku(X509Certificate2 certificate)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509EnhancedKeyUsageExtension eku)
                {
                    foreach (var oid in eku.EnhancedKeyUsages)
                    {
                        if (oid.Value == ServerAuthOid)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
