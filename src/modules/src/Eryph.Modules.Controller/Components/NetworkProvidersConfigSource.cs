using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Serves the <see cref="ConfigDomain.NetworkProviders"/> payload — the network-provider configuration
/// the controller owns — for entitled agents to persist to their local network provider settings.
/// </summary>
internal sealed class NetworkProvidersConfigSource(
    INetworkProviderManager networkProviderManager,
    ILogger<NetworkProvidersConfigSource> logger)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.NetworkProviders;

    public async Task<string> BuildPayloadAsync(string scope, CancellationToken cancellationToken)
    {
        var config = await networkProviderManager.GetCurrentConfiguration()
            .Match(
                c => c,
                error =>
                {
                    // Never distribute an empty/partial network config — that would wipe an agent's
                    // networking. Fail the round instead; agents keep their current copy until the
                    // controller's config is readable again.
                    logger.LogError(
                        "Failed to read network provider configuration for distribution: {Error}.", error.Message);
                    throw new InvalidOperationException(
                        $"Cannot distribute network provider configuration: {error.Message}");
                });

        // Strip the IP-pool NextIp cursor (runtime allocation state, not distributed config) — the same
        // as the authored path — so an ordinary IP allocation does not change the payload and re-push the
        // whole fleet on every allocation.
        foreach (var provider in config.NetworkProviders ?? [])
        foreach (var subnet in provider.Subnets ?? [])
        foreach (var pool in subnet.IpPools ?? [])
            pool.NextIp = null;

        return NetworkProvidersConfigYamlSerializer.Serialize(config);
    }
}
