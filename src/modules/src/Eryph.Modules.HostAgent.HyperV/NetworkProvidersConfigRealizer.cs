using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Applies the controller-distributed network-provider configuration by persisting it
/// to the agent's local network provider settings (today <c>p_networks.yml</c>). The
/// payload is the controller's verbatim YAML, so it is written back unchanged. The
/// host's networking is rebuilt from this file by the existing startup/sync path; a
/// distribution-triggered rebuild is a separate slice.
/// </summary>
internal sealed class NetworkProvidersConfigRealizer(
    INetworkProviderManager networkProviderManager,
    ILogger<NetworkProvidersConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.NetworkProviders;

    public async Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        await networkProviderManager.SaveConfigurationYaml(payload).Match(
            _ => { },
            error => throw new InvalidOperationException(
                $"Failed to persist network provider configuration: {error.Message}"));

        logger.LogInformation(
            "Applied network provider configuration v{Version} to the local agent settings.", version);
    }
}
