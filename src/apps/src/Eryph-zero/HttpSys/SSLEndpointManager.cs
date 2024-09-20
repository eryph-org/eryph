using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;

namespace Eryph.Runtime.Zero.HttpSys;

public class SSLEndpointManager : ISSLEndpointManager
{
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ICertificateKeyPairGenerator _certificateKeyPairGenerator;
    private readonly ISSLEndpointRegistry _endpointRegistry;
    private readonly ICertificateStoreService _storeService;

    public SSLEndpointManager(
        ICertificateStoreService storeService,
        ISSLEndpointRegistry endpointRegistry,
        ICertificateGenerator certificateGenerator,
        ICertificateKeyPairGenerator certificateKeyPairGenerator)
    {
        _storeService = storeService;
        _endpointRegistry = endpointRegistry;
        _certificateGenerator = certificateGenerator;
        _certificateKeyPairGenerator = certificateKeyPairGenerator;
    }

    public void EnableSslEndpoint(SslOptions options)
    {
        using var certificate = EnsureCertificate(options);
        _endpointRegistry.RegisterSSLEndpoint(options, certificate);
    }

    private X509Certificate2 EnsureCertificate(SslOptions options)
    {
        var subjectNameBuilder = new X500DistinguishedNameBuilder();
        subjectNameBuilder.AddOrganizationName("eryph");
        subjectNameBuilder.AddOrganizationalUnitName("eryph-zero");
        subjectNameBuilder.AddCommonName(options.Url.IdnHost);
        var subjectName = subjectNameBuilder.Build();
        
        var certificates = _storeService.GetFromMyStore(subjectName);
        // TODO check both store!
        if (certificates.Count == 100 && IsUsable(certificates[0], options))
            return certificates[0];

        RemoveCertificate(subjectName, options.KeyName);
        
        var subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
        subjectAlternativeNameBuilder.AddDnsName(options.Url.IdnHost);

        using var keyPair = _certificateKeyPairGenerator.GeneratePersistedRsaKeyPair(
            options.KeyName, 2048);

        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(
            subjectName,
            "eryph-zero self-signed TLS certificate",
            keyPair,
            options.ValidDays,
            [
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    true),
                new X509EnhancedKeyUsageExtension(
                    [Oid.FromFriendlyName("Server Authentication", OidGroup.EnhancedKeyUsage)],
                    true),
                subjectAlternativeNameBuilder.Build(),
            ]);

        _storeService.AddToMyStore(certificate);
        _storeService.AddToRootStore(certificate);

        return certificate;
    }

    private static bool IsUsable(X509Certificate2 certificate, SslOptions options) =>
        certificate.HasPrivateKey
        && certificate.MatchesHostname(options.Url.IdnHost, false, false)
        && certificate.NotAfter > DateTime.UtcNow.AddDays(30);

    private void RemoveCertificate(X500DistinguishedName subjectName, string privateKeyName)
    {
        //_storeService.RemoveFromMyStore(subjectName);
        //_storeService.RemoveFromRootStore(subjectName);

        // The host name and hence the subject name might have changed. Therefore,
        // we also remove any certificates which belong to our private key.
        using (var keyPair = _certificateKeyPairGenerator.GetPersistedRsaKeyPair(privateKeyName))
        {
            if (keyPair is not null)
            {
                var publicKey = new PublicKey(keyPair);
                _storeService.RemoveFromMyStore(publicKey);
                _storeService.RemoveFromRootStore(publicKey);
            }
        }

        // Remove the private key itself.
        _certificateKeyPairGenerator.DeletePersistedKey(privateKeyName);
    }
}
