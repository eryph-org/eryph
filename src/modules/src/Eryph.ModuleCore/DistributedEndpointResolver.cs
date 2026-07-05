using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Eryph.ModuleCore;

/// <summary>
/// An <see cref="IEndpointResolver"/> whose endpoint map is supplied at runtime by the
/// controller's <c>Endpoints</c> configuration domain (rather than fixed at process
/// start like <see cref="EndpointResolver"/>). The map is swapped atomically so reads
/// never observe a partially updated set.
/// </summary>
public sealed class DistributedEndpointResolver : IEndpointResolver
{
    private volatile IReadOnlyDictionary<string, string> _endpoints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public Uri GetEndpoint(string name)
    {
        var endpoints = _endpoints;

        Uri? endpoint = null;
        var isDefault = false;
        if (endpoints.TryGetValue(name, out var endpointString))
        {
            var isAbsolute =
                endpointString.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || endpointString.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            endpoint = new Uri(endpointString, isAbsolute ? UriKind.Absolute : UriKind.Relative);
        }

        if (endpoint is null)
        {
            if (!endpoints.TryGetValue("default", out var defaultString))
                throw new InvalidOperationException(
                    $"No endpoint '{name}' is known and no default endpoint has been distributed yet.");
            endpoint = new Uri(defaultString);
            isDefault = true;
        }

        if (endpoint.IsAbsoluteUri || isDefault)
            return endpoint;

        // A named endpoint was found but is relative; resolve it against the default base.
        if (!endpoints.TryGetValue("default", out var defaultBase))
            throw new InvalidOperationException(
                $"Endpoint '{name}' is relative but no default endpoint has been distributed yet.");
        return new Uri(new Uri(defaultBase), endpoint);
    }

    /// <summary>
    /// Reads the raw distributed value for <paramref name="name"/> without the URI coercion
    /// <see cref="GetEndpoint"/> applies — for consumers whose endpoint is not an HTTP(S) URL, such
    /// as the OVN <c>ssl:host:port</c> database endpoints. Returns <see langword="false"/> when the
    /// name has not been distributed (or was distributed empty).
    /// </summary>
    public bool TryGetRawEndpoint(string name, [NotNullWhen(true)] out string? value) =>
        _endpoints.TryGetValue(name, out value) && !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Raised after <see cref="Update"/> swaps in a new endpoint map, so a consumer that realizes an
    /// endpoint into live system state (e.g. the OVN chassis southbound dial) can re-apply when the
    /// controller distributes a change after the consumer already ran once at startup.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>Replaces the endpoint map with the controller-distributed set.</summary>
    public void Update(IReadOnlyDictionary<string, string> endpoints)
    {
        var updated = new Dictionary<string, string>(endpoints, StringComparer.OrdinalIgnoreCase);
        var previous = _endpoints;
        _endpoints = updated;

        // Only signal consumers when the set actually changed. Realizing an endpoint into live system
        // state (DNS resolution, a certificate read, an OVS chassis-plan round-trip) is expensive, and
        // the controller may republish an unchanged map under a new config version.
        if (!SameEndpoints(previous, updated))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool SameEndpoints(
        IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other)
                || !string.Equals(value, other, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
