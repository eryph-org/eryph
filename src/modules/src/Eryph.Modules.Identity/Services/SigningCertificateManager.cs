using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    public class SigningCertificateManager : ISigningCertificateManager
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
            var nameBuilder = new X500DistinguishedNameBuilder();
            nameBuilder.AddOrganizationName("eryph");
            nameBuilder.AddOrganizationalUnitName("identity");
            nameBuilder.AddCommonName("eryph-identity-signing");

            var res = await EnsureCertificateExists(
                Path.Combine(storePath, "identity-signing.key"), nameBuilder.Build());
            return res;
            /*
            var cert = new X509Certificate2(DotNetUtilities.ToX509Certificate(res.certificate));
            var rsaParameters = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)res.keyPair.Private);
            var rsa = RSA.Create(rsaParameters);
            return cert.CopyWithPrivateKey(rsa);
            */
        }

        private async Task<X509Certificate2> EnsureCertificateExists(string privateKeyFile, X500DistinguishedName issuerName)
        {

            //var entropy = Encoding.UTF8.GetBytes(issuerName.ToString());
            //var keyPair = await _cryptoIOServices.TryReadPrivateKeyFile(privateKeyFile, entropy);


            var certs = _storeService.GetFromMyStore2(issuerName);
            if (certs.Count == 1)
            {
                return certs[0];
            }

            foreach (var cert in certs)
            {
                _storeService.RemoveFromMyStore2(cert);
            }

            var certificate = _certificateGenerator.GenerateSelfSignedCertificate2(
                issuerName,
                5 * 365,
                2048);

            _storeService.AddToMyStore(certificate);
            //await _cryptoIOServices.WritePrivateKeyFile(privateKeyFile, keyPair, entropy);

            return certificate;
        }
    }
}