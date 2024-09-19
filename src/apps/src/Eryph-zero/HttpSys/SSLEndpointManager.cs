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
        var certificate = EnsureCertificate(options);
        return _endpointRegistry.RegisterSSLEndpoint(options, certificate);
    }

    private X509Certificate2 EnsureCertificate(SslOptions options)
    {
        var subjectNameBuilder = new X500DistinguishedNameBuilder();
        subjectNameBuilder.AddOrganizationName("eryph");
        subjectNameBuilder.AddOrganizationalUnitName("eryph-zero");
        subjectNameBuilder.AddCommonName(options.SubjectDnsName);

        var subjectName = subjectNameBuilder.Build();

        // TODO remove soon-to-expire certificates
        // TODO remove certificates with different host name
        var certs = _storeService.GetFromMyStore2(subjectName);
        if (certs.Count == 1)
            return certs[0];

        var subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
        subjectAlternativeNameBuilder.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNameBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        subjectAlternativeNameBuilder.AddDnsName("localhost");
        subjectAlternativeNameBuilder.AddDnsName(options.SubjectDnsName);

        var keyPair = _certificateKeyPairGenerator.GenerateProtectedRsaKeyPair(2048);

        var cert = _certificateGenerator.GenerateSelfSignedCertificate(
            subjectName,
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
}
