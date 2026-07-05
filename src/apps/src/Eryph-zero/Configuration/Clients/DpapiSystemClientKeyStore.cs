using System;
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

    // The DPAPI entropy is a STABLE identity identifier: the identity endpoint with the port removed.
    // eryph-zero binds a dynamic port (configured host ':0' picks a free port), so including the port
    // would change the entropy on every restart that lands on a different port and needlessly rotate the
    // credential. Normalizing it to scheme+host+path keeps the key valid across restarts. The
    // out-of-process client tooling reads the identity endpoint from the eryph-zero .lock file and must
    // apply the same normalization to decrypt.
    private byte[] Entropy =>
        Encoding.UTF8.GetBytes(NormalizeEntropy(endpointResolver.GetEndpoint("identity")));

    private static string NormalizeEntropy(Uri endpoint) =>
        new UriBuilder(endpoint) { Port = -1 }.Uri.ToString();

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
