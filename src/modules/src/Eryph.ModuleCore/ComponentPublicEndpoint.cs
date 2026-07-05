using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Eryph.ModuleCore;

/// <summary>
/// A component's public access URL (config key <c>endpoints:public</c>) — the URL at which clients and
/// peer components reach it. It is the single source for both the component's advertised endpoint and
/// the host name baked into its server certificate. It is deliberately independent of
/// <c>ASPNETCORE_URLS</c> (the listening address), so a reverse proxy / load balancer can front the
/// component. Every ASP.NET-serving component configures it the same way; there is no per-component key.
/// </summary>
public static class ComponentPublicEndpoint
{
    private const string PublicEndpointKey = "endpoints:public";
    private const string ServerDnsNamesKey = "componentMtls:serverDnsNames";

    /// <summary>The public access URL, or <see langword="null"/> when not configured.</summary>
    public static Uri? Get(IConfiguration configuration) => Parse(configuration[PublicEndpointKey]);

    /// <summary>
    /// The public access URL read from the environment (<c>endpoints__public</c>), for hosts that
    /// build their endpoint map before an <see cref="IConfiguration"/> is available.
    /// </summary>
    public static Uri? GetFromEnvironment() =>
        Parse(Environment.GetEnvironmentVariable("endpoints__public"));

    /// <summary>
    /// The server-certificate DNS names: the explicit <c>componentMtls:serverDnsNames</c> override
    /// (comma-separated, for a multi-SAN certificate) when set; otherwise the single host of the public
    /// access URL; otherwise empty (the caller falls back to the component FQDN).
    /// </summary>
    public static IReadOnlyList<string> GetServerDnsNames(IConfiguration configuration)
    {
        var explicitNames = Split(configuration[ServerDnsNamesKey]);
        if (explicitNames.Count > 0)
            return explicitNames;

        var url = Get(configuration);
        return url is null ? [] : [url.Host];
    }

    private static Uri? Parse(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;

    private static IReadOnlyList<string> Split(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
