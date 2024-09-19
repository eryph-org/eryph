using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Services
{
    public class SigningCertificateManager : ISigningCertificateManager
    {
        private readonly ICertificateGenerator _certificateGenerator;
        private readonly ICertificateKeyPairGenerator _certificateKeyPairGenerator;
        private readonly ICertificateStoreService _storeService;

        public SigningCertificateManager(
            ICertificateStoreService storeService,
            ICertificateGenerator certificateGenerator,
            ICertificateKeyPairGenerator certificateKeyPairGenerator)
        {
            _storeService = storeService;
            _certificateGenerator = certificateGenerator;
            _certificateKeyPairGenerator = certificateKeyPairGenerator;
        }

        public X509Certificate2 GetSigningCertificate()
        {
            var nameBuilder = new X500DistinguishedNameBuilder();
            nameBuilder.AddOrganizationName("eryph");
            nameBuilder.AddOrganizationalUnitName("identity");
            nameBuilder.AddCommonName("eryph-identity-signing");

            return GetCertificate(nameBuilder.Build(), X509KeyUsageFlags.DigitalSignature);
        }

        public X509Certificate2 GetEncryptionCertificate()
        {
            var nameBuilder = new X500DistinguishedNameBuilder();
            nameBuilder.AddOrganizationName("eryph");
            nameBuilder.AddOrganizationalUnitName("identity");
            nameBuilder.AddCommonName("eryph-identity-encryption");

            return GetCertificate(nameBuilder.Build(), X509KeyUsageFlags.KeyEncipherment);
        }

        private X509Certificate2 GetCertificate(
            X500DistinguishedName issuerName,
            X509KeyUsageFlags keyUsages)
        {
            var certs = _storeService.GetFromMyStore(issuerName);
            if (certs.Count == 1)
            {
                return certs[0];
            }

            foreach (var cert in certs)
            {
                _storeService.RemoveFromMyStore(cert);
            }

            var keyPair = _certificateKeyPairGenerator.GenerateProtectedRsaKeyPair(2048);

            var certificate = _certificateGenerator.GenerateSelfSignedCertificate(
                issuerName,
                keyPair,
                5 * 365,
                [new X509KeyUsageExtension(keyUsages, true)]);

            _storeService.AddToMyStore(certificate);

            return certificate;
        }
    }
}
