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
/// <c>system-client.key</c> at the client-config path. This preserves the on-disk contract the eryph
/// client tooling depends on — same path, same DPAPI protection, and the identity endpoint URL as the
/// protection entropy — that <c>SystemClientGenerator</c> established before the bootstrap moved into
/// the identity module.
/// </summary>
internal sealed class DpapiSystemClientKeyStore(
    ICryptoIOServices cryptoIOServices,
    IEndpointResolver endpointResolver)
    : ISystemClientKeyStore
{
    private static string KeyFile => Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

    // The DPAPI entropy is the identity endpoint URL, exactly as SystemClientGenerator used it; the
    // out-of-process client tooling reads the same endpoint (from the eryph-zero .lock file) to decrypt.
    private byte[] Entropy =>
        Encoding.UTF8.GetBytes(endpointResolver.GetEndpoint("identity").ToString());

    public Task<RSA?> TryReadKey(CancellationToken cancellationToken) =>
        cryptoIOServices.TryReadPrivateKeyFile(KeyFile, Entropy);

    public Task WriteKey(RSA key, CancellationToken cancellationToken) =>
        cryptoIOServices.WritePrivateKeyFile(KeyFile, key, Entropy);
}
