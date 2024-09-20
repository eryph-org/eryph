using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;
using LanguageExt;

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


    public async Task<SSLEndpointContext> EnableSslEndpoint(SslOptions options)
    {
        using var certificate = EnsureCertificate(options);
        return _endpointRegistry.RegisterSSLEndpoint(options, certificate);
    }

    private X509Certificate2 EnsureCertificate(SslOptions options)
    {
        var subjectNameBuilder = new X500DistinguishedNameBuilder();
        subjectNameBuilder.AddOrganizationName("eryph");
        subjectNameBuilder.AddOrganizationalUnitName("eryph-zero");
        // We always use localhost as the common name for the certificate.
        // Otherwise, we cannot clean up old certificates after the DNS name
        // has changed.
        subjectNameBuilder.AddCommonName("localhost");

        var subjectName = subjectNameBuilder.Build();
        var certificates = _storeService.GetFromMyStore(subjectName);
        if (certificates.Count == 1 && IsUsable(certificates[0], options))
            return certificates[0];

        _storeService.RemoveFromMyStore(subjectName);
        _storeService.RemoveFromRootStore(subjectName);
        
        var subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
        subjectAlternativeNameBuilder.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNameBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        subjectAlternativeNameBuilder.AddDnsName("localhost");
        if (options.Url.IdnHost != "localhost")
        {
            subjectAlternativeNameBuilder.AddDnsName(options.Url.IdnHost);
        }
        
        using var keyPair = _certificateKeyPairGenerator.GenerateProtectedRsaKeyPair(2048);

        var cert = _certificateGenerator.GenerateSelfSignedCertificate(
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

        _storeService.AddToMyStore(cert);
        _storeService.AddToRootStore(cert);

        return cert;
    }

    private static bool IsUsable(X509Certificate2 certificate, SslOptions options) =>
        certificate.HasPrivateKey
        && certificate.MatchesHostname(options.Url.IdnHost, false, false)
        && certificate.NotAfter > DateTime.UtcNow.AddDays(30);
}
