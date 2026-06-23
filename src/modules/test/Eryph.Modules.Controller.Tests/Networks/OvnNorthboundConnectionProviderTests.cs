using System;
using FluentAssertions;
using Eryph.Modules.Controller.Networks;
using Xunit;

namespace Eryph.Modules.Controller.Tests.Networks;

public class OvnNorthboundConnectionProviderTests
{
    [Theory]
    [InlineData("ssl:host:6641", "host", 6641)]
    [InlineData("ssl:192.0.2.10:6641", "192.0.2.10", 6641)]
    [InlineData("SSL:host:16641", "host", 16641)]
    [InlineData("ssl:[fe80::1]:6641", "[fe80::1]", 6641)]
    [InlineData("ssl:fe80::1:6641", "fe80::1", 6641)]
    public void ParseSslEndpoint_Valid_ReturnsHostAndPort(string endpoint, string host, int port)
    {
        var result = OvnNorthboundConnectionProvider.ParseSslEndpoint(endpoint);

        result.Host.Should().Be(host);
        result.Port.Should().Be(port);
    }

    [Theory]
    [InlineData("tcp:host:6641")]   // wrong scheme
    [InlineData("ssl:host")]        // no port
    [InlineData("ssl:host:port")]   // non-numeric port
    [InlineData("ssl::6641")]       // empty host
    [InlineData("ssl:   :6641")]    // whitespace-only host
    [InlineData("ssl:host:0")]      // port below range
    [InlineData("ssl:host:99999")]  // port above range
    [InlineData("ssl:host:-5")]     // negative port
    [InlineData("host:6641")]       // no scheme
    public void ParseSslEndpoint_Invalid_Throws(string endpoint)
    {
        var act = () => OvnNorthboundConnectionProvider.ParseSslEndpoint(endpoint);

        act.Should().Throw<InvalidOperationException>();
    }
}
