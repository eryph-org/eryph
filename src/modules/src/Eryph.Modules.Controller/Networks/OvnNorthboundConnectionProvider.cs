using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Controller.Components;
using LanguageExt;
using Microsoft.Extensions.Logging;
using SimpleInjector;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

/// <summary>
/// Resolves the connection the controller uses to reach the OVN northbound database. When the network
/// process runs on the same host (in-process eryph-zero, or a co-located standalone deployment) the
/// controller uses the local pipe — the databases and the controller share the host filesystem. When
/// the network process runs on a different host, the controller dials its advertised SSL endpoint,
/// authenticating with the controller's own enrolled component certificate (single component CA).
/// </summary>
internal interface IOvnNorthboundConnectionProvider
{
    Aff<OvsDbConnection> GetNorthboundConnection();
}

internal class OvnNorthboundConnectionProvider(
    Container container,
    IComponentRegistryService componentRegistry,
    IOVNSettings ovnSettings,
    ISystemEnvironment systemEnvironment,
    ILogger<OvnNorthboundConnectionProvider> logger)
    : IOvnNorthboundConnectionProvider
{
    public Aff<OvsDbConnection> GetNorthboundConnection() =>
        Aff(async () =>
        {
            var components = await componentRegistry.GetActiveAsync(CancellationToken.None);
            var network = components.FirstOrDefault(c => c.ComponentType == ComponentType.Network);

            // Co-located (in-process / same host) or no network component registered yet: the local pipe.
            if (network is null
                || string.Equals(network.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                return ovnSettings.NorthDBConnection;

            if (!network.AdvertisedEndpoints.TryGetValue(OvnRemoteEndpoints.NorthboundName, out var endpoint)
                || string.IsNullOrWhiteSpace(endpoint))
            {
                logger.LogWarning(
                    "The network component on '{Host}' advertised no '{Endpoint}' endpoint; falling back "
                    + "to the local pipe.", network.MachineName, OvnRemoteEndpoints.NorthboundName);
                return ovnSettings.NorthDBConnection;
            }

            return await BuildSslConnection(endpoint);
        });

    // The advertised endpoint has the form "ssl:<host>:<port>".
    private async Task<OvsDbConnection> BuildSslConnection(string endpoint)
    {
        var parts = endpoint.Split(':');
        if (parts.Length != 3
            || !string.Equals(parts[0], "ssl", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(parts[2], out var port))
            throw new InvalidOperationException(
                $"The advertised northbound endpoint '{endpoint}' is not of the form 'ssl:host:port'.");
        var host = parts[1];

        var pem = container.GetInstance<IComponentCertificateStore>().ReadClientCertificatePem()
            ?? throw new InvalidOperationException(
                "The controller is not enrolled, so it cannot present a client certificate to the remote "
                + "OVN northbound database. Enable componentMtls on the controller.");

        // ovn-nbctl reads the client key/certificate/CA from files resolved under the OVN data root, so
        // materialise the controller's enrolled PEMs there. Refreshed on every resolve so a renewed
        // certificate is picked up without a restart.
        var keyFile = new OvsFile("eryph-ovn-client", "nb-client.key");
        var certFile = new OvsFile("eryph-ovn-client", "nb-client.crt");
        var caFile = new OvsFile("eryph-ovn-client", "nb-ca.pem");
        var fileSystem = systemEnvironment.FileSystem;
        fileSystem.EnsurePathForFileExists(keyFile, adminOnly: true);
        await fileSystem.WriteFileAsync(keyFile, pem.PrivateKeyPem);
        await fileSystem.WriteFileAsync(certFile, pem.CertificatePem);
        await fileSystem.WriteFileAsync(caFile, pem.CaBundlePem);

        return new OvsDbConnection(host, port, keyFile, certFile, caFile);
    }
}
