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

    public IReadOnlyList<X509Certificate2> GetIssuingCaCertificates()
    {
        // Public-only copies of every currently valid issuing intermediate (the key-bearing instances
        // stay in the store). Both tiers are returned so the same set can build a chain for either a
        // client or a server leaf; an unrelated intermediate in the ExtraStore is simply ignored.
        //
        // ALL valid generations are returned, not just the newest: during a CA rotation overlap a leaf
        // issued under the previous intermediate must still chain (e.g. to validate it at renewal), so
        // the superseded-but-still-valid intermediate has to be available alongside the new one.
        var result = new List<X509Certificate2>();
        foreach (var tier in new[] { ClientCa, ServerCa })
        {
            // Ensure the tier exists (issues it on first use), then collect every valid generation.
            GetIntermediate(tier).Dispose();
            foreach (var certificate in storeService.GetFromMyStore(tier.SubjectName))
            {
                if (IsValidCa(certificate))
                    result.Add(X509CertificateLoader.LoadCertificate(certificate.RawData));
                certificate.Dispose();
            }
        }
        return result;
    }

    public void RotateRootCertificateAuthority()
    {
        // Demote the current generation (root + both intermediates) to public-only: it stays in the
        // trust bundle so leaves issued under it still validate during the overlap, but it no longer has
        // a private key, so it can never be selected for signing again. Demoting first also guarantees
        // the freshly generated generation below is the ONLY key-bearing one, so signing switches to it
        // deterministically rather than depending on a NotBefore tie-break.
        DemoteToPublicOnly(Root);
        DemoteToPublicOnly(ClientCa);
        DemoteToPublicOnly(ServerCa);

        // Generate a fresh signing root and mint new intermediates from it. Mint directly (not via
        // GetIntermediate) because GetIntermediate's re-issue path removes the whole tier subject first,
        // which would also delete the just-demoted previous intermediates we must keep trusted for the
        // overlap. New leaves now chain through the new intermediates to the new root.
        using (var rootKey = certificateKeyService.GeneratePersistedRsaKey(Root.KeyName, 4096))
        using (var newRoot = certificateGenerator.GenerateCaCertificate(
                   Root.SubjectName, Root.FriendlyName, rootKey, RootValidDays, []))
        {
            storeService.AddToMyStore(newRoot);
        }

        using var signingRoot = EnsureRoot();
        MintIntermediate(ClientCa, signingRoot).Dispose();
        MintIntermediate(ServerCa, signingRoot).Dispose();
    }

    public void RetireSupersededCertificateAuthorities()
    {
        // Drop the demoted (public-only) generations once the overlap is complete — i.e. every component
        // has rotated to a leaf under the new generation — leaving only the current signing generation
        // trusted. Removing a still-in-use old generation would break not-yet-rotated leaves, so this is
        // an explicit "overlap complete" step, separate from RotateRootCertificateAuthority.
        RetireSuperseded(Root);
        RetireSuperseded(ClientCa);
        RetireSuperseded(ServerCa);
    }

    // Replaces every key-bearing certificate of a tier with a public-only copy: it remains trusted (still
    // in the store, still returned by GetTrustedCaCertificates / GetIssuingCaCertificates) but can no
    // longer sign, because the signing key is selected from the store and only key-bearing certificates
    // are candidates.
    private void DemoteToPublicOnly(CaTier tier)
    {
        foreach (var certificate in storeService.GetFromMyStore(tier.SubjectName))
        {
            if (certificate.HasPrivateKey)
            {
                using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
                // Remove by public key (matched on Subject Key Identifier) so only this exact generation
                // is replaced, not other generations sharing the tier subject name.
                storeService.RemoveFromMyStore(certificate.PublicKey);
                storeService.AddToMyStore(publicOnly);
            }
            certificate.Dispose();
        }
    }

    // Removes every generation of a tier except the current signing one (the newest valid key-bearing
    // certificate). Each non-kept generation is removed by its own public key, so the kept one (a
    // different key/SKI) is never touched.
    private void RetireSuperseded(CaTier tier)
    {
        var candidates = storeService.GetFromMyStore(tier.SubjectName);
        var keep = candidates
            .Where(c => IsValidCa(c) && c.HasPrivateKey)
            .OrderByDescending(c => c.NotBefore)
            .FirstOrDefault();
        try
        {
            // No current signing generation (e.g. nothing to retire): leave the trust set untouched
            // rather than wiping it.
            if (keep is null)
                return;

            foreach (var certificate in candidates)
                if (!ReferenceEquals(certificate, keep))
                    storeService.RemoveFromMyStore(certificate.PublicKey);
        }
        finally
        {
            foreach (var certificate in candidates)
                certificate.Dispose();
        }
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

        return MintIntermediate(tier, root);
    }

    // Issues and persists a new key-bearing intermediate of the given tier signed by the supplied root,
    // WITHOUT touching any existing certificates of that tier. GetIntermediate calls this after clearing
    // the tier; rotation calls it directly so the superseded (demoted) intermediates are preserved.
    private X509Certificate2 MintIntermediate(CaTier tier, X509Certificate2 root)
    {
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

    private static bool IsValidCa(X509Certificate2 certificate)
    {
        // Check both validity bounds in UTC. NotBefore/NotAfter are DateTimeKind.Local and DateTime
        // comparison ignores Kind, so a raw comparison would be wrong by the host's UTC offset; and a
        // not-yet-valid CA (future NotBefore) must not be selected for signing or put in the trust bundle,
        // or issued leaves would fail chain validation until its validity begins.
        var now = DateTime.UtcNow;
        return certificate.NotBefore.ToUniversalTime() <= now
            && certificate.NotAfter.ToUniversalTime() > now
            && certificate.Extensions.OfType<X509BasicConstraintsExtension>()
                .Any(e => e.CertificateAuthority);
    }

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
