#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Test.Services;

/// <summary>In-memory <see cref="ICertificateStoreService"/> for unit tests (no machine store).</summary>
internal sealed class InMemoryCertificateStore : ICertificateStoreService
{
    private readonly List<X509Certificate2> _certificates = [];

    // Store an independent clone, like the real File/Windows stores (which serialize the certificate to
    // disk / the OS store on add): the caller keeps ownership of the instance it passed in and may
    // dispose it without affecting what the store retains.
    public void AddToMyStore(X509Certificate2 certificate) => _certificates.Add(Clone(certificate));

    // Return independent clones, exactly like the real File/Windows stores (which load fresh handles
    // from disk / the OS store on every call). The caller owns and may dispose the returned instances
    // without affecting the certificates the store retains — the contract the production code relies on.
    public IReadOnlyList<X509Certificate2> GetFromMyStore(X500DistinguishedName subjectName) =>
        _certificates.Where(c => c.SubjectName.RawData.SequenceEqual(subjectName.RawData))
            .Select(Clone).ToList();

    public void RemoveFromMyStore(X500DistinguishedName subjectName) =>
        RemoveAndDispose(c => c.SubjectName.RawData.SequenceEqual(subjectName.RawData));

    public void RemoveFromMyStore(PublicKey subjectKey)
    {
        // Match by Subject Key Identifier, exactly like the production FileCertificateStoreService —
        // comparing raw key encodings is unreliable across cert/key round-trips.
        var ski = new X509SubjectKeyIdentifierExtension(subjectKey, false).SubjectKeyIdentifier;
        RemoveAndDispose(c =>
            c.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault() is { } certSki
            && string.Equals(certSki.SubjectKeyIdentifier, ski, StringComparison.OrdinalIgnoreCase));
    }

    // The component CA only uses the "my" store; root-store operations are not exercised here.
    public void AddToRootStore(X509Certificate2 certificate) => throw new NotSupportedException();

    public IReadOnlyList<X509Certificate2> GetFromRootStore(X500DistinguishedName subjectName) => [];

    public void RemoveFromRootStore(X500DistinguishedName subjectName) => throw new NotSupportedException();

    public void RemoveFromRootStore(PublicKey subjectKey) => throw new NotSupportedException();

    private static X509Certificate2 Clone(X509Certificate2 certificate) =>
        // Keep the key in-memory (EphemeralKeySet) so this "in-memory" store never persists keys to the
        // Windows user key store, and Exportable so the clone can itself be cloned again (the store clones
        // on both add and read; a non-exportable key would make a second export throw).
        X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pkcs12),
            null,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

    // Dispose the certificates as they are removed so the in-memory store does not leak native handles
    // across the test run (it retains its own clones; removed ones are no longer referenced).
    private void RemoveAndDispose(Predicate<X509Certificate2> match) =>
        _certificates.RemoveAll(c =>
        {
            if (!match(c))
                return false;
            c.Dispose();
            return true;
        });
}

/// <summary>In-memory <see cref="ICertificateKeyService"/> that retains generated keys by name.</summary>
internal sealed class InMemoryKeyService : ICertificateKeyService
{
    private readonly Dictionary<string, RSA> _keys = [];

    public RSA GenerateRsaKey(int keyLength) => RSA.Create(keyLength);

    public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
    {
        // Dispose any key being replaced so repeated generations under one name don't leak handles.
        if (_keys.TryGetValue(keyName, out var existing))
            existing.Dispose();
        var key = RSA.Create(keyLength);
        _keys[keyName] = key;
        // Return an independent copy, like the real FileCertificateKeyService (which persists the key and
        // hands back a fresh RSA the caller owns and disposes); disposing it must not dispose the store's
        // retained instance, or a later GetPersistedRsaKey would touch a disposed key.
        return Clone(key);
    }

    // Return a fresh instance, like the real FileCertificateKeyService (which reloads the key from disk
    // each call): a caller that disposes the returned key must not destroy the store's copy.
    public RSA? GetPersistedRsaKey(string keyName) =>
        _keys.TryGetValue(keyName, out var stored) ? Clone(stored) : null;

    public void DeletePersistedKey(string keyName)
    {
        if (_keys.Remove(keyName, out var key))
            key.Dispose();
    }

    private static RSA Clone(RSA key)
    {
        var copy = RSA.Create();
        copy.ImportRSAPrivateKey(key.ExportRSAPrivateKey(), out _);
        return copy;
    }
}
