using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Eryph.Runtime.Zero.HttpSys;

public class SSLEndpointManager : ISSLEndpointManager
{
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ICryptoIOServices _cryptoIOServices;
    private readonly ISSLEndpointRegistry _endpointRegistry;
    private readonly ICertificateStoreService _storeService;

    public SSLEndpointManager(
        ICryptoIOServices cryptoIOServices,
        ICertificateStoreService storeService,
        ISSLEndpointRegistry endpointRegistry,
        ICertificateGenerator certificateGenerator)
    {
        _cryptoIOServices = cryptoIOServices;
        _storeService = storeService;
        _endpointRegistry = endpointRegistry;
        _certificateGenerator = certificateGenerator;
    }


    public async Task<SSLEndpointContext> EnableSslEndpoint(SSLOptions options)
    {
        var (newCaCertificate, caCertificate, caKeyPair) = await EnsureAuthorityExists(options.RootCertificate);

        var certificates =
            _storeService.GetFromMyStore(caCertificate.SubjectDN).ToArray();

        X509Certificate certificate = null;

        if (certificates.Length == 1)
            certificate = certificates[0];
        else
            //remove all certs in case of duplicates and recreate
            foreach (var deleteCertificate in certificates)
                _storeService.RemoveFromMyStore(deleteCertificate);

        if (certificate != null)
        {
            var valid = certificate.VerifyLocal();

            if (!valid || newCaCertificate)
            {
                _storeService.RemoveFromMyStore(certificate);
                certificate = null;
            }
        }

        certificate ??= CreateSSLCertificate(options.Certificate, caCertificate, caKeyPair);
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate)));
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.Build(new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate)));

        return _endpointRegistry.RegisterSSLEndpoint(options, chain);
    }

    private X509Certificate CreateSSLCertificate(CertificateOptions options,
        X509Certificate caCertificate,
        AsymmetricCipherKeyPair caKeyPair)
    {
        var validEndDate = options.ValidStartDate.AddDays(options.ValidityDays);

        var (certificate, keyPair) = _certificateGenerator.GenerateCertificate(
            caKeyPair,
            caCertificate,
            options.Name,
            validEndDate,
            2048,
            cfg =>
            {
                cfg.AddExtension(
                    X509Extensions.BasicConstraints.Id, true, new BasicConstraints(false));

                cfg.AddExtension(
                    X509Extensions.KeyUsage, true, new KeyUsage(
                        KeyUsage.NonRepudiation | KeyUsage.DigitalSignature |
                        KeyUsage.KeyEncipherment));

                cfg.AddExtension(
                    X509Extensions.ExtendedKeyUsage, true, new ExtendedKeyUsage(
                        KeyPurposeID.IdKPServerAuth
                    ));

                var subjectAltNames = new GeneralNames(new[]
                {
                    new GeneralName(GeneralName.IPAddress, IPAddress.Loopback.ToString()),
                    new GeneralName(GeneralName.IPAddress, IPAddress.IPv6Loopback.ToString()),
                    new GeneralName(GeneralName.DnsName, "localhost"),
                    new GeneralName(GeneralName.DnsName, options.DnsName)
                });
                cfg.AddExtension(
                    X509Extensions.SubjectAlternativeName, false, subjectAltNames);
            }
        );


        _storeService.AddToMyStore(certificate, keyPair);

        return certificate;
    }

    private async Task<(bool newCertificate,
        X509Certificate caCertificate,
        AsymmetricCipherKeyPair keyPair)> EnsureAuthorityExists(RootCertificateOptions options)
    {
        X509Certificate caCertificate = null;

        var privateKeyFile = Path.Combine(options.ExportDirectory, $"{options.FileName}.key");
        var keyPair = await _cryptoIOServices.TryReadPrivateKeyFile(privateKeyFile);

        if (keyPair != null)
        {
            var certs = _storeService.GetFromRootStore(options.Name);
            foreach (var cert in certs)
            {
                if (cert.GetPublicKey().Equals(keyPair.Public))
                {
                    caCertificate = cert;
                    continue;
                }

                _storeService.RemoveFromRootStore(cert);
            }

            if (caCertificate != null)
                return (false, caCertificate, keyPair);
        }

        (caCertificate, keyPair) = _certificateGenerator.GenerateSelfSignedCertificate(
            options.Name,
            5 * 365,
            2048,
            cfg =>
            {
                cfg.AddExtension(
                    X509Extensions.BasicConstraints.Id, true, new BasicConstraints(1));

                cfg.AddExtension(
                    X509Extensions.KeyUsage, true, new KeyUsage(
                        KeyUsage.KeyCertSign | KeyUsage.CrlSign |
                        KeyUsage.DigitalSignature));
            });

        _storeService.AddAsRootCertificate(caCertificate);
        await _cryptoIOServices.WritePrivateKeyFile(privateKeyFile, keyPair);

        return (true, caCertificate, keyPair);
    }
}