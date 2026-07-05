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
        // Only a genuinely absent key returns null (first boot ⇒ the bootstrap generates one). A key that
        // exists but cannot be read (I/O error, wrong format) is surfaced, not swallowed: silently
        // returning null there would make the bootstrap mint a NEW break-glass credential and overwrite
        // the operator's, so failing startup loudly is the safer choice. Atomic owner-only writes make a
        // torn file unlikely in the first place.
        if (!File.Exists(keyFile))
            return null;

        var pem = await File.ReadAllTextAsync(keyFile, Encoding.UTF8, cancellationToken);
        var key = RSA.Create();
        try
        {
            key.ImportFromPem(pem);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public Task WriteKey(RSA key, CancellationToken cancellationToken)
    {
        SecureFile.CreateOwnerOnlyDirectory(Path.GetDirectoryName(keyFile)!);
        SecureFile.WriteOwnerOnly(keyFile, Encoding.UTF8.GetBytes(key.ExportRSAPrivateKeyPem()));
        return Task.CompletedTask;
    }
}
