using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Microsoft.Extensions.Configuration;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Serves the <see cref="ConfigDomain.Endpoints"/> payload — the deployment's service
/// endpoints (e.g. <c>identity</c>, <c>compute</c>, <c>base</c>) as a name→URL map,
/// serialized as JSON.
/// </summary>
/// <remarks>
/// The canonical value of each endpoint is <c>operator-override ?? advertised-by-host</c>:
/// the controller starts from the endpoints components advertised on registration, then
/// overlays the operator override read from the controller's <c>endpoints</c> configuration
/// section. The override always wins because it is authoritative for endpoints whose real
/// access point a hosting component cannot know (load balancer / reverse proxy) — notably
/// the identity issuer URL. The payload is built deterministically (sorted keys) so the
/// distribution version only bumps on a real change.
/// </remarks>
internal sealed class EndpointsConfigSource(
    IConfiguration configuration,
    Container container) : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.Endpoints;

    public async Task<string> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        var endpoints = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Endpoints advertised by the components that host them. A logical endpoint is
        // owned by one host; if several advertise the same name the last one wins, but the
        // operator override below has the final say either way. The registry is scoped, so
        // resolve it in a dedicated scope (this source may be built outside a request scope).
        await using (var scope = AsyncScopedLifestyle.BeginScope(container))
        {
            var registry = scope.GetInstance<IComponentRegistryService>();
            var components = await registry.GetActiveAsync(cancellationToken);
            // Order deterministically so a duplicate advertised endpoint name resolves the same
            // way every build (GetActiveAsync gives no ordering guarantee) — otherwise the payload
            // could flap and bump the version with no real change.
            foreach (var component in components.OrderBy(c => c.ComponentId))
            {
                foreach (var endpoint in component.AdvertisedEndpoints)
                    endpoints[endpoint.Key] = endpoint.Value;
            }
        }

        // Operator override: the controller's "endpoints" configuration section,
        // e.g. endpoints:identity=https://host/identity. Always wins.
        foreach (var child in configuration.GetSection("endpoints").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
                endpoints[child.Key] = child.Value;
        }

        // The resolvers treat "default" as the fallback base for unknown and for relative
        // endpoints. If nothing set it explicitly, derive it from "base" so consumers always
        // have a base to fall back to; an explicitly configured "default" is left untouched.
        if (!endpoints.ContainsKey("default") && endpoints.TryGetValue("base", out var baseEndpoint))
            endpoints["default"] = baseEndpoint;

        return JsonSerializer.Serialize(endpoints);
    }
}
