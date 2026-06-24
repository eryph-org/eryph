using System;
using System.Collections.Generic;

namespace Eryph.ModuleCore;

public class EndpointResolver(Dictionary<string, string> endpoints) : IEndpointResolver
{
    public Uri GetEndpoint(string name)
    {
        Uri? endpoint = null;
        var isDefault = false;
        if (endpoints.TryGetValue(name, out var endpointString))
        {
            endpoint = endpointString.StartsWith("http")
                ? new Uri(endpointString, UriKind.Absolute)
                : new Uri(endpointString, UriKind.Relative);
        }

        if (endpoint == null)
        {
            endpoint = new Uri(endpoints["default"]);
            isDefault = true;
        }

        if (endpoint.IsAbsoluteUri || isDefault) return endpoint;

        var defaultEndpoint = new Uri(endpoints["default"]);
        return new Uri(defaultEndpoint, endpoint);
    }
}
