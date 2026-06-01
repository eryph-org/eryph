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
        // Ensure a signing root exists; the (key-bearing) instance it returns is not needed here, so
        // dispose it rather than leak its handle.
        EnsureRoot().Dispose();
        var candidates = storeService.GetFromMyStore(Root.SubjectName);
        var trusted = candidates
            .Where(IsValidCa)
            .OrderByDescending(c => c.NotBefore)
            .ToList();
        // GetFromMyStore hands out fresh handles each call; dispose the ones we filter out (invalid or
        // expired generations) — the caller owns and disposes the certificates we return.
        foreach (var certificate in candidates)
            if (!trusted.Any(t => ReferenceEquals(t, certificate)))
                certificate.Dispose();
        return trusted;
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
        IReadOnlyList<string> dnsNames, RSA subjectPublicKey, int validDays = 90)
    {
        if (dnsNames is null || dnsNames.Count == 0 || dnsNames.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty server DNS name must be provided.", nameof(dnsNames));

        var subject = new X500DistinguishedNameBuilder();
        subject.AddOrganizationName("eryph");
        subject.AddOrganizationalUnitName("server");
        subject.AddCommonName(dnsNames[0]);

        var san = new SubjectAlternativeNameBuilder();
        foreach (var dnsName in dnsNames)
            san.AddDnsName(dnsName);

        return Issue(ServerCa, subject.Build(), $"eryph server {dnsNames[0]}", subjectPublicKey, ServerAuthOid, san, validDays);
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
        using var intermediate = GetIntermediate(intermediateTier);
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

        // The component/endpoint must present leaf + intermediate; the root is the trusted anchor. The
        // chain is only ever presented or exported as public certificates, so return a public-only copy
        // of the intermediate — its private key is never handed out with the chain — and dispose the
        // key-bearing instance here (the using) instead of leaking its handle to the caller.
        return new IssuedCertificate
        {
            Leaf = leaf,
            IssuingChain = [X509CertificateLoader.LoadCertificate(intermediate.RawData)],
        };
    }

    private X509Certificate2 EnsureRoot()
    {
        var candidates = storeService.GetFromMyStore(Root.SubjectName);
        var signingRoot = candidates
            .Where(c => IsValidCa(c) && c.HasPrivateKey)
            .OrderByDescending(c => c.NotBefore)
            .FirstOrDefault();
        // Dispose every certificate we are not returning — filtered-out/older generations (a rollover
        // can leave several in the store) each hold an unmanaged key handle that would otherwise leak on
        // this long-running process. When none is selected, this disposes them all.
        DisposeExcept(candidates, signingRoot);
        if (signingRoot is not null)
            return signingRoot;

        RemoveTier(Root);
        // The intermediates were signed by the old root's key and would no longer chain to the new root
        // (nor to the now-only-trusted new root returned by GetTrustedCaCertificates). Remove them so the
        // next GetIntermediate re-issues them from the new root; otherwise leaves issued after a root
        // regeneration would fail chain validation against the recovered trust anchor.
        RemoveTier(ClientCa);
        RemoveTier(ServerCa);
        using var key = certificateKeyService.GeneratePersistedRsaKey(Root.KeyName, 4096);
        var root = certificateGenerator.GenerateCaCertificate(
            Root.SubjectName, Root.FriendlyName, key, RootValidDays, []);
        storeService.AddToMyStore(root);
        return root;
    }

    private X509Certificate2 GetIntermediate(CaTier tier)
    {
        // Ensure the root first: if it had to be regenerated it also removed the now-orphaned
        // intermediates (signed by the old root), so the lookup below correctly finds none and re-issues
        // this intermediate from the new root. Reusing an existing intermediate without this check could
        // return one chaining to a root that is no longer trusted. The handle is disposed at method end.
        using var root = EnsureRoot();

        var candidates = storeService.GetFromMyStore(tier.SubjectName);
        var existing = candidates
            .Where(c => IsValidCa(c) && c.HasPrivateKey)
            .OrderByDescending(c => c.NotBefore)
            .FirstOrDefault();
        // Dispose the certificates we are not returning (see EnsureRoot) so intermediate rollovers do
        // not accumulate native handles.
        DisposeExcept(candidates, existing);
        if (existing is not null)
            return existing;

        RemoveTier(tier);

        // The keyless issued intermediate holds a native handle we do not return (intermediateWithKey is
        // the one handed back and persisted), so dispose it after binding the key.
        using var key = certificateKeyService.GeneratePersistedRsaKey(tier.KeyName, 4096);
        using var intermediate = certificateGenerator.IssueIntermediateCaCertificate(
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

    // Dispose every certificate in the set except the one we keep, releasing the unmanaged key handles
    // of the candidates we did not select (passing null for kept disposes them all).
    private static void DisposeExcept(
        System.Collections.Generic.IReadOnlyList<X509Certificate2> certificates, X509Certificate2? kept)
    {
        foreach (var certificate in certificates)
            if (!ReferenceEquals(certificate, kept))
                certificate.Dispose();
    }

    private static bool IsValidCa(X509Certificate2 certificate) =>
        // NotAfter is DateTimeKind.Local and DateTime comparison ignores Kind, so compare in UTC — on a
        // non-UTC host a raw NotAfter > UtcNow would treat the CA as valid past its true expiry (or expired
        // early) by the host's offset. Matches CertificateGenerator and TryLoadLeaf.
        certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow
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
