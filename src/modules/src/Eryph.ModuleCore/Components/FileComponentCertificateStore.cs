using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Stores the component's enrolled certificate material as PEM files in a directory: the leaf
/// certificate, its PKCS#8 private key, the issuing chain and the CA trust bundle. The directory
/// is expected to be ACL-restricted by the deployment tooling (it holds the private key); this
/// type does not relax permissions.
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
        EnsureOwnerOnlyDirectory();
        File.WriteAllText(LeafPath, PemEncoding.WriteString("CERTIFICATE", result.Certificate));
        File.WriteAllText(KeyPath, PemEncoding.WriteString("PRIVATE KEY", clientPkcs8PrivateKey));
        File.WriteAllText(ChainPath, ConcatPem(result.IssuingChain));
        File.WriteAllText(BundlePath, ConcatPem(result.CaTrustBundle));
        WritePfx(LeafPath, KeyPath, ChainPath, PfxPath);

        // The server-TLS certificate (for the component's own endpoint) is optional.
        if (result.ServerCertificate is { Length: > 0 })
        {
            File.WriteAllText(ServerLeafPath, PemEncoding.WriteString("CERTIFICATE", result.ServerCertificate));
            File.WriteAllText(ServerKeyPath, PemEncoding.WriteString("PRIVATE KEY", serverPkcs8PrivateKey));
            File.WriteAllText(ServerChainPath, ConcatPem(result.ServerIssuingChain));
            WritePfx(ServerLeafPath, ServerKeyPath, ServerChainPath, ServerPfxPath);
        }
    }

    /// <summary>
    /// The path to the client certificate as a PKCS#12 file, which the RabbitMQ transport hands to
    /// the TLS stack (Rebus' SslSettings consumes a certificate file, not an in-memory certificate).
    /// Returns null if the component has not been enrolled; (re)creates the file from the stored PEM
    /// if it is missing. The file is unprotected (no passphrase) and relies on the directory ACL,
    /// exactly like the stored private key.
    /// </summary>
    public string? GetClientCertificatePfxPath()
    {
        if (!File.Exists(LeafPath) || !File.Exists(KeyPath))
            return null;
        if (!File.Exists(PfxPath))
            WritePfx(LeafPath, KeyPath, ChainPath, PfxPath);
        return PfxPath;
    }

    /// <summary>
    /// The path to the component's server-TLS certificate as a PKCS#12 file (for its own HTTPS
    /// listener), or null if no server certificate was enrolled.
    /// </summary>
    public string? GetServerCertificatePfxPath()
    {
        if (!File.Exists(ServerLeafPath) || !File.Exists(ServerKeyPath))
            return null;
        if (!File.Exists(ServerPfxPath))
            WritePfx(ServerLeafPath, ServerKeyPath, ServerChainPath, ServerPfxPath);
        return ServerPfxPath;
    }

    public X509Certificate2? LoadClientCertificate()
    {
        if (!File.Exists(LeafPath) || !File.Exists(KeyPath))
            return null;

        using var fromPem = X509Certificate2.CreateFromPemFile(LeafPath, KeyPath);
        // Re-import via PKCS#12 so the private key is usable by the TLS stack: a certificate produced
        // directly from PEM is not usable for TLS handshakes on Windows. DefaultKeySet (not
        // EphemeralKeySet) is required — Schannel cannot use ephemeral keys and fails the handshake
        // with "the platform does not support ephemeral keys". The key lives in the user key store
        // for the lifetime of the returned certificate and is removed when it is disposed.
        return X509CertificateLoader.LoadPkcs12(
            fromPem.Export(X509ContentType.Pkcs12),
            password: null,
            keyStorageFlags: X509KeyStorageFlags.DefaultKeySet);
    }

    // Export a PKCS#12 containing the leaf (with its private key) AND the issuing chain, so the
    // presenting party offers leaf + intermediate(s) and a peer that trusts only the root can build the
    // chain without already holding the intermediates. Loading the file with LoadPkcs12 still returns
    // the key-holding leaf (the chain certs carry no private key).
    private static void WritePfx(string leafPath, string keyPath, string chainPath, string pfxPath)
    {
        var collection = new X509Certificate2Collection();
        try
        {
            collection.Add(X509Certificate2.CreateFromPemFile(leafPath, keyPath));
            if (File.Exists(chainPath))
                collection.ImportFromPemFile(chainPath);

            // Write to a temp file and move into place so a crash mid-write cannot leave a truncated
            // PFX that would later be loaded as a certificate.
            var tempPath = pfxPath + ".tmp";
            File.WriteAllBytes(tempPath, collection.Export(X509ContentType.Pkcs12)!);
            File.Move(tempPath, pfxPath, overwrite: true);
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

    private bool TryLoadLeaf(out DateTime notAfterUtc)
    {
        notAfterUtc = default;
        if (!File.Exists(LeafPath))
            return false;

        try
        {
            using var leaf = X509Certificate2.CreateFromPem(File.ReadAllText(LeafPath));
            notAfterUtc = leaf.NotAfter.ToUniversalTime();
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    // The directory holds private keys; create it owner-only on first creation (0700 on Unix) so the
    // key files are not reachable by other users even if their own mode is permissive. An existing
    // directory's permissions are left to the deployment tooling.
    private void EnsureOwnerOnlyDirectory()
    {
        if (!OperatingSystem.IsWindows() && !Directory.Exists(directory))
            Directory.CreateDirectory(
                directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else
            Directory.CreateDirectory(directory);
    }

    private static string ConcatPem(System.Collections.Generic.IReadOnlyList<byte[]> certificates) =>
        string.Join(
            Environment.NewLine,
            certificates.Select(der => PemEncoding.WriteString("CERTIFICATE", der)));
}
