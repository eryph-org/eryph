using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Serves the <see cref="ConfigDomain.NetworkProviders"/> payload — the raw
/// network-provider YAML the controller owns. The payload is distributed verbatim so
/// an entitled agent can persist it to its local network provider settings without a
/// lossy model round-trip.
/// </summary>
internal sealed class NetworkProvidersConfigSource(
    INetworkProviderManager networkProviderManager,
    ILogger<NetworkProvidersConfigSource> logger)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.NetworkProviders;

    public Task<string> BuildPayloadAsync(CancellationToken cancellationToken) =>
        networkProviderManager.GetCurrentConfigurationYaml()
            .Match(
                Right: yaml => yaml,
                Left: error =>
                {
                    // Never distribute an empty/partial network config — that would wipe an
                    // agent's networking. Fail the round instead; agents keep their current
                    // copy until the controller's config is readable again.
                    logger.LogError(
                        "Failed to read network provider configuration for distribution: {Error}.", error.Message);
                    throw new InvalidOperationException(
                        $"Cannot distribute network provider configuration: {error.Message}");
                });
}
