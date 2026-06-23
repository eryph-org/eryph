using System;
using System.Globalization;
using System.IO;
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
            // Pick the most recently heartbeating Network component deterministically: the registry
            // ages stale registrations out of the active set, but should two ever overlap (e.g. during
            // a split-out migration) the freshest one is the live process to talk to.
            var network = components
                .Where(c => c.ComponentType == ComponentType.Network)
                .OrderByDescending(c => c.LastHeartbeat)
                .FirstOrDefault();

            // Co-located (in-process / same host) or no network component registered yet: the local pipe.
            if (network is null || IsColocated(network.MachineName, ComponentIdentity.GetLocalHostId()))
                return ovnSettings.NorthDBConnection;

            // The network component runs on a different host, so the controller no longer hosts OVN
            // and the local pipe would not reach the northbound database. If the (remote) component has
            // not advertised its endpoint yet, fail fast with an actionable message instead of dialing
            // the local pipe — that would mask the misconfiguration behind an obscure connection error.
            // The Aff is retried by callers, so a component that advertises late heals on the next try.
            if (!network.AdvertisedEndpoints.TryGetValue(OvnRemoteEndpoints.NorthboundName, out var endpoint)
                || string.IsNullOrWhiteSpace(endpoint))
            {
                logger.LogWarning(
                    "The network component on '{Host}' advertised no '{Endpoint}' endpoint yet.",
                    network.MachineName, OvnRemoteEndpoints.NorthboundName);
                throw new InvalidOperationException(
                    $"The network component on '{network.MachineName}' has not advertised its "
                    + $"'{OvnRemoteEndpoints.NorthboundName}' endpoint. The controller cannot reach the "
                    + "remote OVN northbound database; retry once the component advertises its endpoints "
                    + "(ensure componentMtls is enabled on the network component).");
            }

            return await BuildSslConnection(endpoint);
        });

    // Whether a registered component runs on this controller's host. Both names are the local host
    // identity in the same form — the lower-cased FQDN from ComponentIdentity — so the network
    // component's MachineName must be compared against ComponentIdentity.GetLocalHostId(), NOT
    // Environment.MachineName: the short machine name never equals the component's FQDN on a
    // domain-joined host, which would wrongly classify a co-located network module as remote and then
    // fail because a co-located module advertises no remote endpoints. Case-insensitive, as DNS is.
    internal static bool IsColocated(string componentMachineName, string localHostId) =>
        string.Equals(componentMachineName, localHostId, StringComparison.OrdinalIgnoreCase);

    // The advertised endpoint has the form "ssl:<host>:<port>". The host may itself contain ':'
    // (an IPv6 literal), so the port is taken from the last ':' rather than splitting on every ':'.
    internal static (string Host, int Port) ParseSslEndpoint(string endpoint)
    {
        const string prefix = "ssl:";
        // Tolerate surrounding whitespace on the whole value (e.g. a stray trailing newline from
        // config), but reject a missing/whitespace host, whitespace around the port, and an
        // out-of-range port so a malformed endpoint fails here with a clear message rather than
        // producing an OvsDbConnection that fails obscurely on connect (NumberStyles.None rejects the
        // leading/trailing whitespace and sign that int.TryParse would otherwise accept).
        var trimmed = (endpoint ?? "").Trim();
        var hostPort = trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[prefix.Length..]
            : throw new InvalidOperationException(
                $"The advertised northbound endpoint '{endpoint}' must be of the form 'ssl:host:port'.");
        var lastColon = hostPort.LastIndexOf(':');
        var host = lastColon > 0 ? hostPort[..lastColon] : "";
        // An IPv6 literal must be bracketed ("ssl:[fe80::1]:6641"); a bare IPv6 host is ambiguous and
        // OVN rejects it, so reject an unbracketed host that still contains ':' rather than passing it
        // through to build a connection that fails obscurely.
        var unbracketedIpv6 = host.Contains(':') && !(host.StartsWith('[') && host.EndsWith(']'));
        if (lastColon <= 0
            || host.Any(char.IsWhiteSpace)
            || unbracketedIpv6
            || !int.TryParse(hostPort[(lastColon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
            throw new InvalidOperationException(
                $"The advertised northbound endpoint '{endpoint}' is not of the form 'ssl:host:port' "
                + "(an IPv6 host must be bracketed, e.g. 'ssl:[fe80::1]:6641').");
        return (host, port);
    }

    private async Task<OvsDbConnection> BuildSslConnection(string endpoint)
    {
        var (host, port) = ParseSslEndpoint(endpoint);

        // Resolve the store safely so an un-enrolled controller gets the actionable message below rather
        // than a raw container ActivationException.
        var store = container.GetRegistration(typeof(IComponentCertificateStore))?.GetInstance()
            as IComponentCertificateStore;
        var pem = store?.ReadClientCertificatePem()
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
        // The materialised private key must not be readable by other local users. Create the directory
        // owner-restricted up front (the Dbosoft.OVN adminOnly flag is honoured from 2.1.1; this keeps
        // the key protected on 2.1.0 too). Ensure all three files' paths with the same adminOnly flag
        // so the permissions are consistent across platforms and Dbosoft.OVN versions, not just for the
        // key (the cert and CA share the directory).
        var clientCertDirectory = Path.GetDirectoryName(fileSystem.ResolveOvsFilePath(keyFile, false))!;
        SecureDirectory.EnsureOwnerOnly(clientCertDirectory);
        fileSystem.EnsurePathForFileExists(keyFile, adminOnly: true);
        fileSystem.EnsurePathForFileExists(certFile, adminOnly: true);
        fileSystem.EnsurePathForFileExists(caFile, adminOnly: true);
        await fileSystem.WriteFileAsync(keyFile, pem.PrivateKeyPem);
        await fileSystem.WriteFileAsync(certFile, pem.CertificatePem);
        await fileSystem.WriteFileAsync(caFile, pem.CaBundlePem);

        return new OvsDbConnection(host, port, keyFile, certFile, caFile);
    }
}
