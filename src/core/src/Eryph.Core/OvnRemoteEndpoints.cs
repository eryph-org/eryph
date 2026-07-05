using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Eryph.Core;

/// <summary>
/// The well-known names and ports under which the standalone network process exposes the OVN
/// databases to remote clients over SSL. Shared across the components so they agree on a single
/// source: the network process advertises these endpoints on registration and opens the listeners;
/// the controller resolves <see cref="NorthboundName"/> to reach the northbound database and the
/// agents' ovn-controller resolves <see cref="SouthboundName"/> to reach the southbound database
/// when the network process runs on a different host (co-located each uses the local pipe instead).
/// </summary>
public static class OvnRemoteEndpoints
{
    public const int NorthboundPort = 6641;

    public const int SouthboundPort = 6642;

    public const string NorthboundName = "ovn-northbound";

    public const string SouthboundName = "ovn-southbound";

    /// <summary>
    /// Parses an advertised OVN database endpoint of the form <c>ssl:host:port</c> into its host and
    /// port. Shared by the controller (northbound dial) and the agents (southbound dial) so both apply
    /// the same rules. The host may itself contain ':' (an IPv6 literal), so the port is taken from the
    /// last ':' rather than splitting on every ':'.
    /// </summary>
    public static (string Host, int Port) ParseSslEndpoint(string endpoint)
    {
        const string prefix = "ssl:";
        // Tolerate surrounding whitespace on the whole value (e.g. a stray trailing newline from
        // config), but reject a missing/whitespace host, whitespace around the port, and an
        // out-of-range port so a malformed endpoint fails here with a clear message rather than
        // producing an OvsDbConnection that fails obscurely on connect (NumberStyles.None rejects the
        // leading/trailing whitespace and sign that int.TryParse would otherwise accept).
        var trimmed = (endpoint ?? "").Trim();
        var hostPort = trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[prefix.Length..]
            : throw new InvalidOperationException(
                $"The advertised OVN endpoint '{endpoint}' must be of the form 'ssl:host:port'.");
        var lastColon = hostPort.LastIndexOf(':');
        var host = lastColon > 0 ? hostPort[..lastColon] : "";
        // Brackets denote an IPv6 literal ("ssl:[fe80::1]:6641"); their contents must actually be a
        // valid IPv6 address, so reject "ssl:[]:6641" / "ssl:[not-an-ip]:6641" / a bracketed IPv4 rather
        // than passing a host that only fails later on connect. A bare host still containing ':' is an
        // unbracketed IPv6 literal, ambiguous with the port separator, so reject that too.
        var isBracketed = host.StartsWith('[') && host.EndsWith(']');
        var bracketedNotIpv6 = isBracketed
            && !(IPAddress.TryParse(host[1..^1], out var ipv6)
                 && ipv6.AddressFamily == AddressFamily.InterNetworkV6);
        var unbracketedIpv6 = !isBracketed && host.Contains(':');
        if (lastColon <= 0
            || host.Any(char.IsWhiteSpace)
            || unbracketedIpv6
            || bracketedNotIpv6
            || !int.TryParse(hostPort[(lastColon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
            throw new InvalidOperationException(
                $"The advertised OVN endpoint '{endpoint}' is not of the form 'ssl:host:port' "
                + "(an IPv6 host must be bracketed and a valid IPv6 literal, e.g. 'ssl:[fe80::1]:6641').");
        return (host, port);
    }
}
