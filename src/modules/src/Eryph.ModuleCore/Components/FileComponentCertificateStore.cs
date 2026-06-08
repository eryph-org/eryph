using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Stores the component's enrolled certificate material in a directory. The PKCS#12 (<c>component.pfx</c>,
/// bundling leaf + private key + issuing chain) is the authoritative artifact — it is what the bus
/// transport loads, it is written first and atomically, and the validity checks read it. The PEM files
/// (leaf, key, chain, CA bundle) are secondary copies for tooling/inspection. The directory is expected
/// to be ACL-restricted by the deployment tooling (it holds the private key); this type does not relax
/// permissions, and writes key-bearing files owner-only.
/// </summary>
public sealed class FileComponentCertificateStore(string directory, TimeSpan renewalLeadTime)
    : IComponentCertificateStore
{
    private string LeafPath => Path.Combine(directory, "component.crt");
    private string KeyPath => Path.Combine(directory, "component.key");
    private string ChainPath => Path.Combine(directory, "issuing-chain.pem");
    private string BundlePath => Path.Combine(directory, "ca-bundle.pem");
    private string PfxPath => Path.Combine(directory, "component.pfx");
    private string ServerLeafPath => Path.Combine(directory, "server.crt");
    private string ServerKeyPath => Path.Combine(directory, "server.key");
    private string ServerChainPath => Path.Combine(directory, "server-chain.pem");
    private string ServerPfxPath => Path.Combine(directory, "server.pfx");

    public bool HasValidCertificate() =>
        TryLoadLeaf(out var notAfter) && notAfter > DateTime.UtcNow;

    public bool HasCurrentCertificate() =>
        TryLoadLeaf(out var notAfter) && notAfter > DateTime.UtcNow + renewalLeadTime;

    public void Save(byte[] clientPkcs8PrivateKey, byte[] serverPkcs8PrivateKey, ComponentEnrollmentResult result)
    {
        SecureDirectory.EnsureOwnerOnly(directory);

        // Write the PKCS#12 first and atomically, built straight from the in-memory result: it bundles
        // leaf + key + chain and is the only artifact the bus transport loads, so once it lands the
        // component is fully enrolled regardless of the PEM copies. This matters because the enrollment
        // token is single-use — a partial save that left the component looking un-enrolled would force a
        // retry with an already-consumed token and brick the component on the next start.
        WritePfxFromResult(PfxPath, result.Certificate, clientPkcs8PrivateKey, result.IssuingChain);

        // Secondary PEM copies (tooling/inspection, CA trust bundle), each written atomically.
        WritePemOwnerOnly(LeafPath, PemEncoding.WriteString("CERTIFICATE", result.Certificate));
        WriteKeyOwnerOnly(KeyPath, clientPkcs8PrivateKey);
        WritePemOwnerOnly(ChainPath, ConcatPem(result.IssuingChain));
        WritePemOwnerOnly(BundlePath, ConcatPem(result.CaTrustBundle));

        // The server-TLS certificate (for the component's own endpoint) is optional.
        if (result.ServerCertificate is { Length: > 0 })
        {
            if (serverPkcs8PrivateKey is not { Length: > 0 })
                throw new ArgumentException(
                    "A server certificate was provided without its private key.", nameof(serverPkcs8PrivateKey));
            WritePfxFromResult(ServerPfxPath, result.ServerCertificate, serverPkcs8PrivateKey, result.ServerIssuingChain);
            WritePemOwnerOnly(ServerLeafPath, PemEncoding.WriteString("CERTIFICATE", result.ServerCertificate));
            WriteKeyOwnerOnly(ServerKeyPath, serverPkcs8PrivateKey);
            WritePemOwnerOnly(ServerChainPath, ConcatPem(result.ServerIssuingChain));
        }
        else
        {
            // This enrollment carries no server certificate: remove any artifacts a previous one left
            // behind so the component never keeps serving TLS with a stale certificate the current
            // enrollment dropped — the persisted state must match the latest result.
            DeleteIfExists(ServerPfxPath, ServerLeafPath, ServerKeyPath, ServerChainPath);
        }
    }

    // Write a PKCS#8 private key as PEM, owner-only (0600 on Unix) and atomically.
    private static void WriteKeyOwnerOnly(string path, byte[] pkcs8PrivateKey) =>
        WritePemOwnerOnly(path, PemEncoding.WriteString("PRIVATE KEY", pkcs8PrivateKey));

    private static void WritePemOwnerOnly(string path, string pem) =>
        SecureFile.WriteOwnerOnly(path, Encoding.ASCII.GetBytes(pem));

    private static void DeleteIfExists(params string[] paths)
    {
        foreach (var path in paths)
            if (File.Exists(path))
                File.Delete(path);
    }

    /// <summary>
    /// The path to the client certificate as a PKCS#12 file, which the RabbitMQ transport hands to the
    /// TLS stack (Rebus' SslSettings consumes a certificate file, not an in-memory certificate). Returns
    /// the stored PFX when present (the authoritative artifact); if only PEMs were provisioned out of
    /// band, (re)builds the PFX from them. Returns null if the component has not been enrolled.
    /// </summary>
    public string? GetClientCertificatePfxPath() =>
        ResolvePfxPath(PfxPath, LeafPath, KeyPath, ChainPath);

    /// <summary>
    /// The path to the component's server-TLS certificate as a PKCS#12 file (for its own HTTPS
    /// listener), or null if no server certificate was enrolled.
    /// </summary>
    public string? GetServerCertificatePfxPath() =>
        ResolvePfxPath(ServerPfxPath, ServerLeafPath, ServerKeyPath, ServerChainPath);

    private static string? ResolvePfxPath(string pfxPath, string leafPath, string keyPath, string chainPath)
    {
        // The PFX is the source of truth (Save writes it first and atomically).
        if (File.Exists(pfxPath))
            return pfxPath;
        // Out-of-band provisioning may have placed only PEMs; (re)build the PFX from them.
        if (!File.Exists(leafPath) || !File.Exists(keyPath))
            return null;
        WritePfxFromPem(leafPath, keyPath, chainPath, pfxPath);
        return pfxPath;
    }

    public X509Certificate2? LoadClientCertificate()
    {
        // Prefer the authoritative PFX; fall back to PEM leaf+key. DefaultKeySet (not EphemeralKeySet)
        // is required — Schannel cannot use ephemeral keys and fails the handshake with "the platform
        // does not support ephemeral keys". The key lives in the user key store for the lifetime of the
        // returned certificate and is removed when it is disposed.
        if (File.Exists(PfxPath))
            return X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(PfxPath), password: null, keyStorageFlags: X509KeyStorageFlags.DefaultKeySet);

        if (!File.Exists(LeafPath) || !File.Exists(KeyPath))
            return null;

        using var fromPem = X509Certificate2.CreateFromPemFile(LeafPath, KeyPath);
        return X509CertificateLoader.LoadPkcs12(
            fromPem.Export(X509ContentType.Pkcs12),
            password: null,
            keyStorageFlags: X509KeyStorageFlags.DefaultKeySet);
    }

    // Build a PKCS#12 (leaf-with-key + issuing chain) directly from the in-memory enrollment result and
    // write it atomically. Producing it without reading the (separately written) PEM files is what makes
    // a crash between PEM writes safe: the complete PFX is already durable, so the component is not left
    // looking un-enrolled (which would force a retry with the already-consumed one-time token).
    private static void WritePfxFromResult(
        string pfxPath, byte[] certificateDer, byte[] pkcs8PrivateKey, IReadOnlyList<byte[]> chainDer)
    {
        var collection = new X509Certificate2Collection();
        try
        {
            using var key = RSA.Create();
            key.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);
            using var leafNoKey = X509CertificateLoader.LoadCertificate(certificateDer);
            collection.Add(leafNoKey.CopyWithPrivateKey(key));
            foreach (var der in chainDer)
                collection.Add(X509CertificateLoader.LoadCertificate(der));

            // SecureFile writes owner-only (0600 on Unix) and atomically (temp + rename), so a crash
            // mid-write cannot leave a truncated/loose-perm PFX.
            SecureFile.WriteOwnerOnly(pfxPath, collection.Export(X509ContentType.Pkcs12)!);
        }
        finally
        {
            foreach (var certificate in collection)
                certificate.Dispose();
        }
    }

    // Rebuild the PFX from the PEM leaf + key (+ chain). Only used for the out-of-band-PEM fallback in
    // ResolvePfxPath; the enrollment path uses WritePfxFromResult.
    private static void WritePfxFromPem(string leafPath, string keyPath, string chainPath, string pfxPath)
    {
        var collection = new X509Certificate2Collection();
        try
        {
            collection.Add(X509Certificate2.CreateFromPemFile(leafPath, keyPath));
            if (File.Exists(chainPath))
                collection.ImportFromPemFile(chainPath);
            SecureFile.WriteOwnerOnly(pfxPath, collection.Export(X509ContentType.Pkcs12)!);
        }
        finally
        {
            foreach (var certificate in collection)
                certificate.Dispose();
        }
    }

    public X509Certificate2Collection LoadCaTrustBundle()
    {
        var bundle = new X509Certificate2Collection();
        if (File.Exists(BundlePath))
            bundle.ImportFromPemFile(BundlePath);
        return bundle;
    }

    /// <summary>
    /// Reads the enrolled client PEM material (private key, leaf-with-issuing-chain certificate and CA
    /// trust bundle) as strings, or <see langword="null"/> when the component is not yet enrolled. Used
    /// to configure OVN SSL, which takes PEM-encoded strings. The certificate is the leaf followed by the
    /// issuing chain so a peer can build the path to the CA when the component CA is an intermediate.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> if the PEM copies are incomplete — notably the rare case where a
    /// crash between <see cref="Save"/>'s PKCS#12 write and its PEM writes left only the PFX. The CA trust
    /// bundle is not carried by the PFX, so it cannot be reconstructed from it; recovery is a re-enrollment
    /// (which rewrites the full PEM set). Callers must treat null as "not currently usable for SSL".
    /// </remarks>
    public ComponentCertificatePem? ReadClientCertificatePem()
    {
        if (!File.Exists(KeyPath) || !File.Exists(LeafPath) || !File.Exists(BundlePath))
            return null;

        var certificate = File.ReadAllText(LeafPath);
        if (File.Exists(ChainPath))
        {
            var chain = File.ReadAllText(ChainPath);
            if (!string.IsNullOrWhiteSpace(chain))
                certificate = certificate.TrimEnd() + "\n" + chain.TrimStart();
        }

        return new ComponentCertificatePem(
            File.ReadAllText(KeyPath),
            certificate,
            File.ReadAllText(BundlePath));
    }

    private bool TryLoadLeaf(out DateTime notAfterUtc)
    {
        notAfterUtc = default;

        // The PKCS#12 is the source of truth: Save writes it first and atomically, and it is the artifact
        // the transport loads, so a complete, key-bearing PFX means "enrolled and usable" even if the
        // secondary PEM copies are missing (e.g. a crash between writes left only the PFX).
        if (File.Exists(PfxPath))
        {
            try
            {
                // EphemeralKeySet: this is only a validity check (NotAfter + HasPrivateKey), so keep the key
                // in memory rather than importing it into the user key store on every poll/startup check.
                using var leaf = X509CertificateLoader.LoadPkcs12(
                    File.ReadAllBytes(PfxPath), password: null, keyStorageFlags: X509KeyStorageFlags.EphemeralKeySet);
                if (leaf.HasPrivateKey)
                {
                    notAfterUtc = leaf.NotAfter.ToUniversalTime();
                    return true;
                }
            }
            catch (CryptographicException)
            {
                // Unreadable PFX — fall back to the PEM copy.
            }
        }

        // Fall back to the PEM leaf + key. Loading them together also rejects a corrupt or mismatched key
        // (which would otherwise pass an existence check and then fail when the PFX is built), so
        // re-enrollment is triggered instead of failing later when the certificate/key is needed.
        if (!File.Exists(LeafPath) || !File.Exists(KeyPath))
            return false;

        try
        {
            using var leaf = X509Certificate2.CreateFromPemFile(LeafPath, KeyPath);
            notAfterUtc = leaf.NotAfter.ToUniversalTime();
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static string ConcatPem(IReadOnlyList<byte[]> certificates) =>
        // Join with "\n" (not Environment.NewLine): PEM is conventionally LF-terminated, and these files
        // are meant to be readable by external tooling (openssl, etc.) across platforms.
        string.Join(
            "\n",
            certificates.Select(der => PemEncoding.WriteString("CERTIFICATE", der)));
}
