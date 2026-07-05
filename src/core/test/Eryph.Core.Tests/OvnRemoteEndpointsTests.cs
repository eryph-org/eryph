namespace Eryph.Core.Tests;

public class OvnRemoteEndpointsTests
{
    [Theory]
    [InlineData("ssl:host:6641", "host", 6641)]
    [InlineData("ssl:192.0.2.10:6641", "192.0.2.10", 6641)]
    [InlineData("SSL:host:16641", "host", 16641)]
    [InlineData("ssl:[fe80::1]:6641", "[fe80::1]", 6641)] // bracketed IPv6
    [InlineData("  ssl:host:6641\t", "host", 6641)] // surrounding whitespace is trimmed
    public void ParseSslEndpoint_Valid_ReturnsHostAndPort(string endpoint, string host, int port)
    {
        var result = OvnRemoteEndpoints.ParseSslEndpoint(endpoint);

        result.Host.Should().Be(host);
        result.Port.Should().Be(port);
    }

    [Theory]
    [InlineData("tcp:host:6641")] // wrong scheme
    [InlineData("ssl:host")] // no port
    [InlineData("ssl:host:port")] // non-numeric port
    [InlineData("ssl::6641")] // empty host
    [InlineData("ssl:   :6641")] // whitespace-only host
    [InlineData("ssl:ho st:6641")] // whitespace inside the host
    [InlineData("ssl:host: 6641")] // whitespace around the port
    [InlineData("ssl:fe80::1:6641")] // bare (unbracketed) IPv6 host
    [InlineData("ssl:[]:6641")] // empty brackets
    [InlineData("ssl:[not-an-ip]:6641")] // brackets around a non-IPv6 host
    [InlineData("ssl:[10.0.0.1]:6641")] // brackets around an IPv4 literal (brackets are IPv6-only)
    [InlineData("ssl:host:0")] // port below range
    [InlineData("ssl:host:99999")] // port above range
    [InlineData("ssl:host:-5")] // negative port
    [InlineData("host:6641")] // no scheme
    public void ParseSslEndpoint_Invalid_Throws(string endpoint)
    {
        var act = () => OvnRemoteEndpoints.ParseSslEndpoint(endpoint);

        act.Should().Throw<InvalidOperationException>();
    }
}
