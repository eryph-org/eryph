using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Microsoft.Extensions.Configuration;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Serves the <see cref="ConfigDomain.Endpoints"/> payload — the deployment's service
/// endpoints (e.g. <c>identity</c>, <c>compute</c>, <c>base</c>) as a name→URL map,
/// serialized as JSON.
/// </summary>
/// <remarks>
/// Today the canonical value of each endpoint is the operator override read from the
/// controller's <c>endpoints</c> configuration section. This is the authoritative
/// source for endpoints whose real access point a hosting component cannot know
/// (load balancer / reverse proxy) — notably the identity issuer URL. When the
/// standalone API/identity host exists, its advertised endpoint (carried on
/// registration) will fill in any endpoint the operator did not override; the override
/// always wins. The payload is built deterministically (sorted keys) so the
/// distribution version only bumps on a real change.
/// </remarks>
internal sealed class EndpointsConfigSource(IConfiguration configuration) : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.Endpoints;

    public Task<string> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        // Operator override: the controller's "endpoints" configuration section,
        // e.g. endpoints:identity=https://host/identity.
        var endpoints = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in configuration.GetSection("endpoints").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
                endpoints[child.Key] = child.Value;
        }

        return Task.FromResult(JsonSerializer.Serialize(endpoints));
    }
}
