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

    public void AddToMyStore(X509Certificate2 certificate) => _certificates.Add(certificate);

    public IReadOnlyList<X509Certificate2> GetFromMyStore(X500DistinguishedName subjectName) =>
        _certificates.Where(c => c.SubjectName.RawData.SequenceEqual(subjectName.RawData)).ToList();

    public void RemoveFromMyStore(X500DistinguishedName subjectName) =>
        _certificates.RemoveAll(c => c.SubjectName.RawData.SequenceEqual(subjectName.RawData));

    public void RemoveFromMyStore(PublicKey subjectKey)
    {
        // Match by Subject Key Identifier, exactly like the production FileCertificateStoreService —
        // comparing raw key encodings is unreliable across cert/key round-trips.
        var ski = new X509SubjectKeyIdentifierExtension(subjectKey, false).SubjectKeyIdentifier;
        _certificates.RemoveAll(c =>
            c.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault() is { } certSki
            && string.Equals(certSki.SubjectKeyIdentifier, ski, StringComparison.OrdinalIgnoreCase));
    }

    // The component CA only uses the "my" store; root-store operations are not exercised here.
    public void AddToRootStore(X509Certificate2 certificate) => throw new NotSupportedException();

    public IReadOnlyList<X509Certificate2> GetFromRootStore(X500DistinguishedName subjectName) => [];

    public void RemoveFromRootStore(X500DistinguishedName subjectName) => throw new NotSupportedException();

    public void RemoveFromRootStore(PublicKey subjectKey) => throw new NotSupportedException();
}

/// <summary>In-memory <see cref="ICertificateKeyService"/> that retains generated keys by name.</summary>
internal sealed class InMemoryKeyService : ICertificateKeyService
{
    private readonly Dictionary<string, RSA> _keys = [];

    public RSA GenerateRsaKey(int keyLength) => RSA.Create(keyLength);

    public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
    {
        var key = RSA.Create(keyLength);
        _keys[keyName] = key;
        return key;
    }

    public RSA? GetPersistedRsaKey(string keyName) =>
        _keys.TryGetValue(keyName, out var key) ? key : null;

    public void DeletePersistedKey(string keyName) => _keys.Remove(keyName);
}
