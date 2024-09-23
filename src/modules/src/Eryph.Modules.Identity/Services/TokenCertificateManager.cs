using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Services;

public class TokenCertificateManager(
    ICertificateStoreService storeService,
    ICertificateGenerator certificateGenerator,
    ICertificateKeyService certificateKeyService)
    : ITokenCertificateManager
{
    public X509Certificate2 GetSigningCertificate()
    {
        var nameBuilder = new X500DistinguishedNameBuilder();
        nameBuilder.AddOrganizationName("eryph");
        nameBuilder.AddOrganizationalUnitName("identity");
        nameBuilder.AddCommonName("eryph-identity-signing");

        return GetCertificate(
            nameBuilder.Build(),
            "eryph-identity-signing-key",
            "eryph identity token signing",
            X509KeyUsageFlags.DigitalSignature);
    }

    public X509Certificate2 GetEncryptionCertificate()
    {
        var nameBuilder = new X500DistinguishedNameBuilder();
        nameBuilder.AddOrganizationName("eryph");
        nameBuilder.AddOrganizationalUnitName("identity");
        nameBuilder.AddCommonName("eryph-identity-encryption");

        return GetCertificate(
            nameBuilder.Build(),
            "eryph-identity-encryption-key",
            "eryph identity token encryption",
            X509KeyUsageFlags.KeyEncipherment);
    }

    private X509Certificate2 GetCertificate(
        X500DistinguishedName subjectName,
        string keyName,
        string friendlyName,
        X509KeyUsageFlags keyUsage)
    {
        var certificates = storeService.GetFromMyStore(subjectName);
        if (certificates.Count == 1 && IsUsable(certificates[0], keyUsage))
            return certificates[0];

        RemoveCertificate(subjectName, keyName);

        using var keyPair = certificateKeyService.GeneratePersistedRsaKey(
            keyName, 2048);

        var certificate = certificateGenerator.GenerateSelfSignedCertificate(
            subjectName,
            friendlyName,
            keyPair,
            5 * 365,
            [
                new X509KeyUsageExtension(keyUsage, true),
            ]);

        storeService.AddToMyStore(certificate);

        return certificate;
    }

    private static bool IsUsable(X509Certificate2 certificate, X509KeyUsageFlags keyUsage) =>
        certificate.HasPrivateKey
        && certificate.NotAfter > DateTime.UtcNow.AddDays(30)
        && certificate.Extensions.OfType<X509KeyUsageExtension>()
            .Any(e => e.KeyUsages.HasFlag(keyUsage));

    private void RemoveCertificate(X500DistinguishedName subjectName, string keyName)
    {
        storeService.RemoveFromMyStore(subjectName);

        // Also remove any certificates with the same key name. They will become
        // unusable as we are going to remove the private key.
        using (var keyPair = certificateKeyService.GetPersistedRsaKey(keyName))
        {
            if (keyPair is not null)
            {
                var publicKey = new PublicKey(keyPair);
                storeService.RemoveFromMyStore(publicKey);
            }
        }

        // Remove the private key itself.
        certificateKeyService.DeletePersistedKey(keyName);
    }
}
