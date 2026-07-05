using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.Identity.Bootstrap;

/// <summary>
/// Host-supplied storage for the <c>system-client</c> private key — the bootstrap super-admin
/// credential. The identity module owns <em>when</em> the key exists and how it maps to the identity
/// database (see <see cref="SystemClientBootstrap"/>); the host owns <em>where</em> it is stored and
/// how it is protected at rest, because that differs per packaging:
/// eryph-zero keeps the existing DPAPI-encrypted file at its client-config path (an external contract
/// its client tooling reads), while the cross-platform standalone identity host writes an owner-only
/// PEM (the operator break-glass key). Keeping this a seam is why the module never references DPAPI (a
/// Windows-only mechanism) or a concrete path.
/// </summary>
public interface ISystemClientKeyStore
{
    /// <summary>
    /// Reads the stored private key, or returns <see langword="null"/> when none is present (or it
    /// cannot be read). The caller owns and disposes the returned key.
    /// </summary>
    Task<RSA?> TryReadKey(CancellationToken cancellationToken);

    /// <summary>Persists <paramref name="key"/> as the system-client private key.</summary>
    Task WriteKey(RSA key, CancellationToken cancellationToken);
}
