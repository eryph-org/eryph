using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Eryph.Security.Cryptography;

/// <summary>
/// Cross-platform <see cref="ICertificateStoreService"/> backed by a directory (the default on
/// non-Windows hosts; Windows uses the machine certificate store). Certificates are stored one file
/// per certificate under <c>&lt;dir&gt;/my</c> and <c>&lt;dir&gt;/root</c>. A certificate that carries
/// its private key is written as a PKCS#12 so that <see cref="GetFromMyStore"/> returns a signable
/// certificate — mirroring the Windows store+CNG linkage the CA and token manager rely on; public-only
/// certificates are written as PEM. Mirrors how .NET's own Linux <see cref="X509Store"/> persists.
/// </summary>
public sealed class FileCertificateStoreService(string directory) : ICertificateStoreService
{
    private string MyDirectory => Path.Combine(directory, "my");
    private string RootDirectory => Path.Combine(directory, "root");

    public void AddToMyStore(X509Certificate2 certificate) => Add(certificate, MyDirectory);

    public void AddToRootStore(X509Certificate2 certificate) => Add(certificate, RootDirectory);

    public IReadOnlyList<X509Certificate2> GetFromMyStore(X500DistinguishedName subjectName) =>
        GetBySubject(MyDirectory, subjectName);

    public IReadOnlyList<X509Certificate2> GetFromRootStore(X500DistinguishedName subjectName) =>
        GetBySubject(RootDirectory, subjectName);

    public void RemoveFromMyStore(X500DistinguishedName subjectName) =>
        Remove(MyDirectory, c => SubjectMatches(c, subjectName));

    public void RemoveFromMyStore(PublicKey subjectKey) =>
        Remove(MyDirectory, c => PublicKeyMatches(c, subjectKey));

    public void RemoveFromRootStore(X500DistinguishedName subjectName) =>
        Remove(RootDirectory, c => SubjectMatches(c, subjectName));

    public void RemoveFromRootStore(PublicKey subjectKey) =>
        Remove(RootDirectory, c => PublicKeyMatches(c, subjectKey));

    private static void Add(X509Certificate2 certificate, string storeDirectory)
    {
        SecureFile.CreateOwnerOnlyDirectory(storeDirectory);

        // Key-bearing certs are persisted as PKCS#12 (so the key survives the round-trip); public-only
        // certs as PEM. The thumbprint disambiguates multiple generations under one subject. The write
        // is owner-only and atomic (the PKCS#12 holds a private key).
        var hasKey = certificate.HasPrivateKey;
        var path = Path.Combine(storeDirectory, certificate.Thumbprint + (hasKey ? ".pfx" : ".crt"));
        var bytes = hasKey
            ? certificate.Export(X509ContentType.Pkcs12)
            : Encoding.ASCII.GetBytes(certificate.ExportCertificatePem());
        SecureFile.WriteOwnerOnly(path, bytes);
    }

    private static IReadOnlyList<X509Certificate2> GetBySubject(
        string storeDirectory, X500DistinguishedName subjectName)
    {
        // Load every cert, keep the matches, and dispose the rest — each loaded cert wraps a native
        // key/cert handle, so the non-matches must not be leaked on a long-running, repeatedly-querying host.
        var matches = new List<X509Certificate2>();
        foreach (var certificate in LoadAll(storeDirectory))
            if (SubjectMatches(certificate, subjectName))
                matches.Add(certificate);
            else
                certificate.Dispose();
        return matches;
    }

    private static void Remove(string storeDirectory, Func<X509Certificate2, bool> predicate)
    {
        if (!Directory.Exists(storeDirectory))
            return;

        foreach (var path in CertificateFiles(storeDirectory))
        {
            X509Certificate2 certificate;
            try
            {
                certificate = Load(path);
            }
            catch
            {
                continue;
            }

            using (certificate)
            {
                if (predicate(certificate))
                    File.Delete(path);
            }
        }
    }

    private static IEnumerable<X509Certificate2> LoadAll(string storeDirectory)
    {
        if (!Directory.Exists(storeDirectory))
            yield break;

        foreach (var path in CertificateFiles(storeDirectory))
        {
            X509Certificate2? certificate = null;
            try
            {
                certificate = Load(path);
            }
            catch
            {
                // Skip unreadable/foreign files rather than failing the whole lookup.
            }

            if (certificate is not null)
                yield return certificate;
        }
    }

    // Only our own artifacts — never the in-flight ".tmp" files SecureFile writes, nor anything else.
    private static IEnumerable<string> CertificateFiles(string storeDirectory) =>
        Directory.EnumerateFiles(storeDirectory, "*.pfx")
            .Concat(Directory.EnumerateFiles(storeDirectory, "*.crt"));

    private static X509Certificate2 Load(string path) =>
        path.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
            ? X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(path), null)
            : X509Certificate2.CreateFromPem(File.ReadAllText(path));

    private static bool SubjectMatches(X509Certificate2 certificate, X500DistinguishedName subjectName) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(subjectName.RawData);

    private static bool PublicKeyMatches(X509Certificate2 certificate, PublicKey subjectKey)
    {
        var ski = new X509SubjectKeyIdentifierExtension(subjectKey, false);
        var certSki = certificate.Extensions
            .OfType<X509SubjectKeyIdentifierExtension>()
            .FirstOrDefault();
        return certSki is not null
               && string.Equals(certSki.SubjectKeyIdentifier, ski.SubjectKeyIdentifier,
                   StringComparison.OrdinalIgnoreCase);
    }
}
