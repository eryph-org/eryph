using System;
using System.Collections.Generic;

namespace Eryph.ModuleCore
{
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

        /// <summary>Replaces the endpoint map with the controller-distributed set.</summary>
        public void Update(IReadOnlyDictionary<string, string> endpoints) =>
            _endpoints = new Dictionary<string, string>(endpoints, StringComparer.OrdinalIgnoreCase);

        public Uri GetEndpoint(string name)
        {
            var endpoints = _endpoints;

            Uri? endpoint = null;
            var isDefault = false;
            if (endpoints.TryGetValue(name, out var endpointString))
            {
                endpoint = endpointString.StartsWith("http")
                    ? new Uri(endpointString, UriKind.Absolute)
                    : new Uri(endpointString, UriKind.Relative);
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
    }
}
