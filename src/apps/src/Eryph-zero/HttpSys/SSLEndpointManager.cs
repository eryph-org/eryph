using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;

namespace Eryph.Runtime.Zero.HttpSys;

public class SslEndpointManager(
    ICertificateStoreService storeService,
    ISslEndpointRegistry endpointRegistry,
    ICertificateGenerator certificateGenerator,
    ICertificateKeyService certificateKeyService)
    : ISslEndpointManager
{
    public void EnableSslEndpoint(SslOptions options)
    {
        using var certificate = EnsureCertificate(options);
        endpointRegistry.RegisterSslEndpoint(options, certificate);
    }

    private X509Certificate2 EnsureCertificate(SslOptions options)
    {
        var subjectNameBuilder = new X500DistinguishedNameBuilder();
        subjectNameBuilder.AddOrganizationName("eryph");
        subjectNameBuilder.AddOrganizationalUnitName("eryph-zero");
        subjectNameBuilder.AddCommonName(options.Url.IdnHost);
        var subjectName = subjectNameBuilder.Build();
        
        var myStoreCertificates = storeService.GetFromMyStore(subjectName);
        var rootStoreCertificates = storeService.GetFromRootStore(subjectName);
        if (myStoreCertificates.Count == 1
            && rootStoreCertificates.Count == 1
            && IsUsable(myStoreCertificates[0], rootStoreCertificates[0], options))
        {
            return myStoreCertificates[0];
        }
        
        RemoveCertificate(subjectName, options.KeyName);
        
        var subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
        subjectAlternativeNameBuilder.AddDnsName(options.Url.IdnHost);

        using var keyPair = certificateKeyService.GeneratePersistedRsaKey(
            options.KeyName, 2048);

        var certificate = certificateGenerator.GenerateSelfSignedCertificate(
            subjectName,
            "eryph-zero self-signed TLS certificate",
            keyPair,
            options.ValidDays,
            [
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    true),
                new X509EnhancedKeyUsageExtension(
                    [Oid.FromOidValue(Oids.EnhancedKeyUsage.ServerAuthentication, OidGroup.EnhancedKeyUsage)],
                    true),
                subjectAlternativeNameBuilder.Build(),
            ]);

        storeService.AddToMyStore(certificate);
        storeService.AddToRootStore(certificate);

        return certificate;
    }

    private static bool IsUsable(
        X509Certificate2 myStoreCertificate,
        X509Certificate2 rootStoreCertificate,
        SslOptions options) =>
        myStoreCertificate.HasPrivateKey
        && myStoreCertificate.Thumbprint == rootStoreCertificate.Thumbprint
        && myStoreCertificate.MatchesHostname(options.Url.IdnHost, false, false)
        && myStoreCertificate.NotAfter > DateTime.UtcNow.AddDays(30);

    private void RemoveCertificate(X500DistinguishedName subjectName, string privateKeyName)
    {
        storeService.RemoveFromMyStore(subjectName);
        storeService.RemoveFromRootStore(subjectName);

        // The host name and hence the subject name might have changed. Therefore,
        // we also remove any certificates which belong to our private key.
        using (var keyPair = certificateKeyService.GetPersistedRsaKey(privateKeyName))
        {
            if (keyPair is not null)
            {
                var publicKey = new PublicKey(keyPair);
                storeService.RemoveFromMyStore(publicKey);
                storeService.RemoveFromRootStore(publicKey);
            }
        }

        // Remove the private key itself.
        certificateKeyService.DeletePersistedKey(privateKeyName);
    }
}
