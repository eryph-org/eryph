using System;
using System.Collections.Generic;

namespace Eryph.ModuleCore
{
    public class EndpointResolver : IEndpointResolver
    {
        private readonly Dictionary<string, string> _endpoints;

        public EndpointResolver(Dictionary<string, string> endpoints)
        {
            _endpoints = endpoints;
        }

        public Uri GetEndpoint(string name)
        {
            Uri? endpoint = null;
            var isDefault = false;
            if (_endpoints.ContainsKey(name))
            {
                var endpointString = _endpoints[name];
                endpoint = endpointString.StartsWith("http")
                    ? new Uri(endpointString, UriKind.Absolute)
                    : new Uri(endpointString, UriKind.Relative);
            }

            if (endpoint == null)
            {
                endpoint = new Uri(_endpoints["default"]);
                isDefault = true;
            }

            if (endpoint.IsAbsoluteUri || isDefault) return endpoint;

            var defaultEndpoint = new Uri(_endpoints["default"]);
            return new Uri(defaultEndpoint, endpoint);
        }
    }
}