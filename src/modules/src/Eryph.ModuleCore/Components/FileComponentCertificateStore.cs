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

    public bool HasValidCertificate() =>
        TryLoadLeaf(out var notAfter) && notAfter > DateTime.UtcNow;

    public bool HasCurrentCertificate() =>
        TryLoadLeaf(out var notAfter) && notAfter > DateTime.UtcNow + renewalLeadTime;

    public void Save(byte[] pkcs8PrivateKey, ComponentEnrollmentResult result)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(LeafPath, PemEncoding.WriteString("CERTIFICATE", result.Certificate));
        File.WriteAllText(KeyPath, PemEncoding.WriteString("PRIVATE KEY", pkcs8PrivateKey));
        File.WriteAllText(ChainPath, ConcatPem(result.IssuingChain));
        File.WriteAllText(BundlePath, ConcatPem(result.CaTrustBundle));
    }

    public X509Certificate2? LoadClientCertificate()
    {
        if (!File.Exists(LeafPath) || !File.Exists(KeyPath))
            return null;

        using var fromPem = X509Certificate2.CreateFromPemFile(LeafPath, KeyPath);
        // Re-import via PKCS#12 so the private key is usable by the TLS stack (a certificate
        // produced directly from PEM is not reliably usable for TLS handshakes on Windows).
        // Ephemeral so no key is left behind in a machine store.
        return X509CertificateLoader.LoadPkcs12(
            fromPem.Export(X509ContentType.Pkcs12),
            password: null,
            keyStorageFlags: X509KeyStorageFlags.EphemeralKeySet);
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

    private static string ConcatPem(System.Collections.Generic.IReadOnlyList<byte[]> certificates) =>
        string.Join(
            Environment.NewLine,
            certificates.Select(der => PemEncoding.WriteString("CERTIFICATE", der)));
}
