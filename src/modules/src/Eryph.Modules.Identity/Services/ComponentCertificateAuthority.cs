using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// The deployment's component PKI. A single self-signed <b>root</b> CA is the trust anchor; two
/// intermediates signed by the root — a <b>client</b> intermediate (component mTLS, clientAuth) and
/// a <b>server-TLS</b> intermediate (serverAuth) — issue the leaves. Using one root for both
/// directions means a single trust anchor validates server and client certificates alike. All CA
/// material is loaded from the certificate store or created on first use (mirroring
/// <see cref="TokenCertificateManager"/>) and reused afterwards.
/// </summary>
public class ComponentCertificateAuthority(
    ICertificateStoreService storeService,
    ICertificateGenerator certificateGenerator,
    ICertificateKeyService certificateKeyService)
    : IComponentCertificateAuthority
{
    // The root must outlive the intermediates, which must outlive the leaves; the generator clamps
    // each to its issuer's lifetime. Leaves are short-lived (~90d, renewed at half life).
    private const int RootValidDays = 10 * 365;
    private const int IntermediateValidDays = 5 * 365;

    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2"; // id-kp-clientAuth
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1"; // id-kp-serverAuth

    private static readonly CaTier Root = new("eryph-component-root-ca", "eryph-component-root-ca-key", "eryph component root CA");
    private static readonly CaTier ClientCa = new("eryph-component-client-ca", "eryph-component-client-ca-key", "eryph component client CA");
    private static readonly CaTier ServerCa = new("eryph-server-tls-ca", "eryph-server-tls-ca-key", "eryph server TLS CA");

    public IReadOnlyList<X509Certificate2> GetTrustedCaCertificates()
    {
        EnsureRoot();
        return storeService.GetFromMyStore(Root.SubjectName)
            .Where(IsValidCa)
            .OrderByDescending(c => c.NotBefore)
            .ToList();
    }

    public IssuedCertificate IssueComponentCertificate(
        string componentId, string fqdn, RSA subjectPublicKey, int validDays = 90)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            throw new ArgumentException("The component id must be provided.", nameof(componentId));
        if (string.IsNullOrWhiteSpace(fqdn))
            throw new ArgumentException("The component FQDN must be provided.", nameof(fqdn));

        var subject = new X500DistinguishedNameBuilder();
        subject.AddOrganizationName("eryph");
        subject.AddOrganizationalUnitName("component");
        subject.AddCommonName(fqdn);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(fqdn);
        // The stable component id as a URN SAN so a peer can map the authenticated certificate to
        // the exact component identity rather than the (human-oriented) CN.
        san.AddUri(new Uri($"urn:eryph:component:{componentId}"));

        return Issue(ClientCa, subject.Build(), $"eryph component {fqdn}", subjectPublicKey, ClientAuthOid, san, validDays);
    }

    public IssuedCertificate IssueServerCertificate(
        string dnsName, RSA subjectPublicKey, int validDays = 90)
    {
        if (string.IsNullOrWhiteSpace(dnsName))
            throw new ArgumentException("The server DNS name must be provided.", nameof(dnsName));

        var subject = new X500DistinguishedNameBuilder();
        subject.AddOrganizationName("eryph");
        subject.AddOrganizationalUnitName("server");
        subject.AddCommonName(dnsName);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);

        return Issue(ServerCa, subject.Build(), $"eryph server {dnsName}", subjectPublicKey, ServerAuthOid, san, validDays);
    }

    private IssuedCertificate Issue(
        CaTier intermediateTier,
        X500DistinguishedName subjectName,
        string friendlyName,
        RSA subjectPublicKey,
        string enhancedKeyUsageOid,
        SubjectAlternativeNameBuilder san,
        int validDays)
    {
        var intermediate = GetIntermediate(intermediateTier);
        var leaf = certificateGenerator.IssueCertificate(
            subjectName,
            friendlyName,
            subjectPublicKey,
            intermediate,
            validDays,
            [
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true),
                new X509EnhancedKeyUsageExtension([new Oid(enhancedKeyUsageOid)], false),
                san.Build(),
            ]);

        // The component/endpoint must present leaf + intermediate; the root is the trusted anchor.
        return new IssuedCertificate { Leaf = leaf, IssuingChain = [intermediate] };
    }

    private X509Certificate2 EnsureRoot()
    {
        var signingRoot = storeService.GetFromMyStore(Root.SubjectName)
            .Where(c => IsValidCa(c) && c.HasPrivateKey)
            .OrderByDescending(c => c.NotBefore)
            .FirstOrDefault();
        if (signingRoot is not null)
            return signingRoot;

        RemoveTier(Root);
        using var key = certificateKeyService.GeneratePersistedRsaKey(Root.KeyName, 4096);
        var root = certificateGenerator.GenerateCaCertificate(
            Root.SubjectName, Root.FriendlyName, key, RootValidDays, []);
        storeService.AddToMyStore(root);
        return root;
    }

    private X509Certificate2 GetIntermediate(CaTier tier)
    {
        var existing = storeService.GetFromMyStore(tier.SubjectName)
            .Where(c => IsValidCa(c) && c.HasPrivateKey)
            .OrderByDescending(c => c.NotBefore)
            .FirstOrDefault();
        if (existing is not null)
            return existing;

        RemoveTier(tier);

        var root = EnsureRoot();
        using var key = certificateKeyService.GeneratePersistedRsaKey(tier.KeyName, 4096);
        var intermediate = certificateGenerator.IssueIntermediateCaCertificate(
            tier.SubjectName, tier.FriendlyName, key, root, IntermediateValidDays, []);

        // The issued intermediate carries no private key (it was signed by the root); bind the
        // intermediate's key so it can sign leaves on this and subsequent loads, then persist it.
        var intermediateWithKey = intermediate.CopyWithPrivateKey(key);
        storeService.AddToMyStore(intermediateWithKey);
        return intermediateWithKey;
    }

    private void RemoveTier(CaTier tier)
    {
        storeService.RemoveFromMyStore(tier.SubjectName);

        using (var key = certificateKeyService.GetPersistedRsaKey(tier.KeyName))
        {
            if (key is not null)
                storeService.RemoveFromMyStore(new PublicKey(key));
        }

        certificateKeyService.DeletePersistedKey(tier.KeyName);
    }

    private static bool IsValidCa(X509Certificate2 certificate) =>
        certificate.NotAfter > DateTime.UtcNow
        && certificate.Extensions.OfType<X509BasicConstraintsExtension>()
            .Any(e => e.CertificateAuthority);

    private sealed record CaTier(string CommonName, string KeyName, string FriendlyName)
    {
        public X500DistinguishedName SubjectName
        {
            get
            {
                var builder = new X500DistinguishedNameBuilder();
                builder.AddOrganizationName("eryph");
                builder.AddOrganizationalUnitName("component-ca");
                builder.AddCommonName(CommonName);
                return builder.Build();
            }
        }
    }
}
