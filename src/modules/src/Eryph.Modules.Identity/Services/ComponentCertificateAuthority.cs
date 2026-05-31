using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Manages the deployment's component CA certificate and issues per-component client
/// certificates from it. The CA certificate is loaded from the certificate store or created on
/// first use (mirroring <see cref="TokenCertificateManager"/>); issued component certificates
/// authenticate components on the message bus via mTLS.
/// </summary>
public class ComponentCertificateAuthority(
    ICertificateStoreService storeService,
    ICertificateGenerator certificateGenerator,
    ICertificateKeyService certificateKeyService)
    : IComponentCertificateAuthority
{
    private const string CaKeyName = "eryph-component-ca-key";
    private const string CaFriendlyName = "eryph component CA";

    // The CA must outlive the component certificates it issues; issued certificates are clamped
    // to the CA's remaining lifetime by the generator. A long-lived CA avoids forcing a
    // re-enrollment of every component when the CA would otherwise expire.
    private const int CaValidDays = 10 * 365;

    // The TLS client-authentication EKU (id-kp-clientAuth); components are TLS clients of the broker.
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";

    public X509Certificate2 GetCaCertificate()
    {
        var subjectName = BuildCaSubjectName();

        var certificates = storeService.GetFromMyStore(subjectName);
        if (certificates.Count == 1 && IsUsableCa(certificates[0]))
            return certificates[0];

        RemoveCertificate(subjectName, CaKeyName);

        using var keyPair = certificateKeyService.GeneratePersistedRsaKey(CaKeyName, 4096);
        var caCertificate = certificateGenerator.GenerateCaCertificate(
            subjectName,
            CaFriendlyName,
            keyPair,
            CaValidDays,
            []);

        storeService.AddToMyStore(caCertificate);

        return caCertificate;
    }

    public X509Certificate2 IssueComponentCertificate(
        string componentId,
        string fqdn,
        RSA subjectPublicKey,
        int validDays = 365)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("The component id must be provided.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(fqdn))
            throw new ArgumentException("The component FQDN must be provided.", nameof(fqdn));

        var caCertificate = GetCaCertificate();

        var subjectNameBuilder = new X500DistinguishedNameBuilder();
        subjectNameBuilder.AddOrganizationName("eryph");
        subjectNameBuilder.AddOrganizationalUnitName("component");
        subjectNameBuilder.AddCommonName(fqdn);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(fqdn);
        // The stable component id as a URN SAN, so a peer can map the authenticated certificate
        // to the exact component identity rather than relying on the (human-oriented) CN.
        sanBuilder.AddUri(new Uri($"urn:eryph:component:{componentId}"));

        var extensions = new List<X509Extension>
        {
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true),
            new X509EnhancedKeyUsageExtension([new Oid(ClientAuthOid)], false),
            sanBuilder.Build(),
        };

        return certificateGenerator.IssueCertificate(
            subjectNameBuilder.Build(),
            $"eryph component {fqdn}",
            subjectPublicKey,
            caCertificate,
            validDays,
            extensions);
    }

    private static X500DistinguishedName BuildCaSubjectName()
    {
        var nameBuilder = new X500DistinguishedNameBuilder();
        nameBuilder.AddOrganizationName("eryph");
        nameBuilder.AddOrganizationalUnitName("component-ca");
        nameBuilder.AddCommonName("eryph-component-ca");
        return nameBuilder.Build();
    }

    private static bool IsUsableCa(X509Certificate2 certificate) =>
        certificate.HasPrivateKey
        && certificate.NotAfter > DateTime.UtcNow.AddDays(30)
        && certificate.Extensions.OfType<X509BasicConstraintsExtension>()
            .Any(e => e.CertificateAuthority);

    private void RemoveCertificate(X500DistinguishedName subjectName, string keyName)
    {
        storeService.RemoveFromMyStore(subjectName);

        // Also remove any certificate bound to the same key name; it becomes unusable once the
        // private key is deleted (mirrors TokenCertificateManager).
        using (var keyPair = certificateKeyService.GetPersistedRsaKey(keyName))
        {
            if (keyPair is not null)
                storeService.RemoveFromMyStore(new PublicKey(keyPair));
        }

        certificateKeyService.DeletePersistedKey(keyName);
    }
}
