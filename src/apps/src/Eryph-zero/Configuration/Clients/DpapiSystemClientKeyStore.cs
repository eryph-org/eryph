using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore;
using Eryph.Modules.Identity.Bootstrap;
using Eryph.Security.Cryptography;

namespace Eryph.Runtime.Zero.Configuration.Clients;

/// <summary>
/// eryph-zero's <see cref="ISystemClientKeyStore"/>: the machine-encrypted (DPAPI, LocalMachine)
/// <c>system-client.key</c> at the client-config path. The eryph client tooling reads and decrypts the
/// same file, so the path, the DPAPI protection, and the entropy are a shared contract.
/// </summary>
internal sealed class DpapiSystemClientKeyStore(
    ICryptoIOServices cryptoIOServices,
    IEndpointResolver endpointResolver)
    : ISystemClientKeyStore
{
    private static string KeyFile => Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

    // The DPAPI entropy is the full identity endpoint (including the port). This is a shared contract:
    // the out-of-process client tooling reads the identity endpoint from the eryph-zero .lock file and
    // uses the same value to decrypt. It must stay byte-for-byte identical to what those clients apply,
    // so do NOT normalize it (e.g. strip the port) — that silently breaks every deployed client's ability
    // to read the key. A dynamic port changing across restarts merely re-mints the key; the client picks
    // up the new endpoint and key together from the .lock file, so it keeps working.
    private byte[] Entropy =>
        Encoding.UTF8.GetBytes(endpointResolver.GetEndpoint("identity").ToString());

    // Returns null on any read failure (missing or corrupt file, or DPAPI decryption failing after a
    // machine change). The bootstrap then regenerates: that self-heal is the long-standing eryph-zero
    // behaviour and is appropriate for its local, disposable, single-machine store, so this store
    // deliberately does not fail loud the way the standalone break-glass file store does (see
    // ISystemClientKeyStore).
    public Task<RSA?> TryReadKey(CancellationToken cancellationToken) =>
        cryptoIOServices.TryReadPrivateKeyFile(KeyFile, Entropy);

    public Task WriteKey(RSA key, CancellationToken cancellationToken) =>
        cryptoIOServices.WritePrivateKeyFile(KeyFile, key, Entropy);
}
