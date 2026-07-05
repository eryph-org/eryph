using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Identity.Bootstrap;
using Eryph.Security.Cryptography;

namespace Eryph.Identity;

/// <summary>
/// The standalone identity host's <see cref="ISystemClientKeyStore"/>: the system-client private key as
/// an owner-only PEM file (0600 on Unix, ACL-restricted on Windows). This is the operator break-glass
/// credential provisioning retrieves. It deliberately does not use DPAPI (Windows-only) — the control
/// node is cross-platform, and the key is protected by file permissions, the same way the identity
/// host already stores its component client certificate.
/// </summary>
internal sealed class FileSystemClientKeyStore(string keyFile) : ISystemClientKeyStore
{
    public async Task<RSA?> TryReadKey(CancellationToken cancellationToken)
    {
        if (!File.Exists(keyFile))
            return null;

        try
        {
            var pem = await File.ReadAllTextAsync(keyFile, Encoding.UTF8, cancellationToken);
            var key = RSA.Create();
            key.ImportFromPem(pem);
            return key;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A missing key is expected on first boot; an unreadable one (torn write, wrong format) is
            // treated the same so the bootstrap regenerates a working credential rather than crashing.
            return null;
        }
    }

    public Task WriteKey(RSA key, CancellationToken cancellationToken)
    {
        SecureFile.CreateOwnerOnlyDirectory(Path.GetDirectoryName(keyFile)!);
        SecureFile.WriteOwnerOnly(keyFile, Encoding.UTF8.GetBytes(key.ExportRSAPrivateKeyPem()));
        return Task.CompletedTask;
    }
}
