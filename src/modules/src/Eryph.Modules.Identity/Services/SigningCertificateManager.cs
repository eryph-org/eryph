using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Eryph.Modules.Identity.Services
{
    public class SigningCertificateManager
    {
        private readonly ICertificateGenerator _certificateGenerator;
        private readonly ICryptoIOServices _cryptoIOServices;
        private readonly ICertificateStoreService _storeService;

        public SigningCertificateManager(
            ICryptoIOServices cryptoIOServices,
            ICertificateStoreService storeService,
            ICertificateGenerator certificateGenerator)
        {
            _cryptoIOServices = cryptoIOServices;
            _storeService = storeService;
            _certificateGenerator = certificateGenerator;
        }

        public async Task<X509Certificate2> GetSigningCertificate(string storePath)
        {
            var res = await EnsureCertificateExists(
                Path.Combine(storePath, "identity-signing.key"), new X509Name("CN=eryph-identity, O=eryph, OU=identity"));
            var cert = new X509Certificate2(DotNetUtilities.ToX509Certificate(res.certificate));
            var rsa = DotNetUtilities.ToRSA(res.keyPair.Private as RsaPrivateCrtKeyParameters);
            return cert.CopyWithPrivateKey(rsa);
        }

        private async Task<(X509Certificate certificate,
            AsymmetricCipherKeyPair keyPair)> EnsureCertificateExists(string privateKeyFile, X509Name issuerName)
        {
            X509Certificate certificate = null;

            var entropy = Encoding.UTF8.GetBytes(issuerName.ToString());

            var keyPair = await _cryptoIOServices.TryReadPrivateKeyFile(privateKeyFile, entropy);

            if (keyPair != null)
            {
                var certs = _storeService.GetFromMyStore(issuerName);
                foreach (var cert in certs)
                {
                    if (cert.GetPublicKey().Equals(keyPair.Public))
                    {
                        certificate = cert;
                        continue;
                    }

                    _storeService.RemoveFromMyStore(cert);
                }

                if (certificate != null)
                    return (certificate, keyPair);
            }

            (certificate, keyPair) = _certificateGenerator.GenerateSelfSignedCertificate(
                issuerName,
                5 * 365,
                2048,
                cfg =>
                {
                    cfg.AddExtension(
                        X509Extensions.BasicConstraints.Id, true, new BasicConstraints(false));

                    cfg.AddExtension(
                        X509Extensions.KeyUsage, true, new KeyUsage(
                            KeyUsage.DigitalSignature));
                });

            _storeService.AddToMyStore(certificate);
            await _cryptoIOServices.WritePrivateKeyFile(privateKeyFile, keyPair, entropy);

            return (certificate, keyPair);
        }
    }
}