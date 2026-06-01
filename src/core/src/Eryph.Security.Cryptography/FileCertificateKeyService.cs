using System;
using System.IO;
using System.Security.Cryptography;

namespace Eryph.Security.Cryptography;

/// <summary>
/// Cross-platform <see cref="ICertificateKeyService"/> that persists RSA keys as PKCS#8 files in a
/// directory. This is the default on non-Windows hosts (Windows uses CNG machine keys); the operator
/// is expected to place the directory on an access-restricted / encrypted volume. File and directory
/// permissions are tightened to owner-only on Unix.
/// </summary>
public sealed class FileCertificateKeyService(string directory) : ICertificateKeyService
{
    public RSA GenerateRsaKey(int keyLength) => RSA.Create(keyLength);

    public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
    {
        var path = KeyPath(keyName);
        var key = RSA.Create(keyLength);
        try
        {
            SecureFile.CreateOwnerOnlyDirectory(directory);
            SecureFile.WriteOwnerOnly(path, key.ExportPkcs8PrivateKey());
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public RSA? GetPersistedRsaKey(string keyName)
    {
        var path = KeyPath(keyName);
        if (!File.Exists(path))
            return null;

        var key = RSA.Create();
        try
        {
            key.ImportPkcs8PrivateKey(File.ReadAllBytes(path), out _);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public void DeletePersistedKey(string keyName)
    {
        var path = KeyPath(keyName);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string KeyPath(string keyName)
    {
        // Reject anything that could escape the key directory; key names are simple identifiers.
        if (string.IsNullOrWhiteSpace(keyName)
            || keyName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || keyName.Contains('/') || keyName.Contains('\\')
            || keyName is "." or "..")
            throw new ArgumentException($"Invalid key name '{keyName}'.", nameof(keyName));
        return Path.Combine(directory, keyName + ".key");
    }
}
