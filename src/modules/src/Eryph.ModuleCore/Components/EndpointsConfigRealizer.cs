using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Applies the controller-distributed <see cref="ConfigDomain.Endpoints"/> map to the
/// local <see cref="DistributedEndpointResolver"/>, so the component resolves the
/// deployment's service endpoints (e.g. the identity issuer) from the controller
/// instead of an in-process resolver. Idempotent: re-applying simply swaps the map.
/// </summary>
public sealed class EndpointsConfigRealizer(
    DistributedEndpointResolver resolver,
    ILogger<EndpointsConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.Endpoints;

    public Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var endpoints = JsonSerializer.Deserialize<Dictionary<string, string>>(payload)
                        ?? new Dictionary<string, string>();
        resolver.Update(endpoints);

        logger.LogInformation(
            "Applied endpoints configuration v{Version}: {Count} endpoint(s) [{Names}].",
            version, endpoints.Count, string.Join(", ", endpoints.Keys));
        return Task.CompletedTask;
    }
}
