using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb.Model;
using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimpleInjector;
using Xunit;

namespace Eryph.Modules.Controller.Tests.Networks;

public class OvnNorthboundConnectionProviderTests
{
    [Theory]
    [InlineData("ssl:host:6641", "host", 6641)]
    [InlineData("ssl:192.0.2.10:6641", "192.0.2.10", 6641)]
    [InlineData("SSL:host:16641", "host", 16641)]
    [InlineData("ssl:[fe80::1]:6641", "[fe80::1]", 6641)]  // bracketed IPv6
    [InlineData("  ssl:host:6641\t", "host", 6641)]  // surrounding whitespace is trimmed
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
    [InlineData("ssl:ho st:6641")]  // whitespace inside the host
    [InlineData("ssl:host: 6641")]  // whitespace around the port
    [InlineData("ssl:fe80::1:6641")] // bare (unbracketed) IPv6 host
    [InlineData("ssl:host:0")]      // port below range
    [InlineData("ssl:host:99999")]  // port above range
    [InlineData("ssl:host:-5")]     // negative port
    [InlineData("host:6641")]       // no scheme
    public void ParseSslEndpoint_Invalid_Throws(string endpoint)
    {
        var act = () => OvnNorthboundConnectionProvider.ParseSslEndpoint(endpoint);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("host.domain.example", "host.domain.example", true)]
    [InlineData("HOST.domain.example", "host.domain.example", true)]    // DNS is case-insensitive
    [InlineData("host", "host.domain.example", false)]                  // short name never equals the FQDN
    [InlineData("other.domain.example", "host.domain.example", false)]  // a genuinely different host
    public void IsColocated_ComparesFullyQualifiedNames(
        string componentMachineName, string localHostId, bool expected)
    {
        OvnNorthboundConnectionProvider.IsColocated(componentMachineName, localHostId)
            .Should().Be(expected);
    }

    [Fact]
    public async Task GetNorthboundConnection_NoNetworkComponent_UsesLocalPipe()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        var provider = CreateProvider([], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        result.IsSucc.Should().BeTrue();
        result.Match(Succ: c => c, Fail: e => throw new Exception(e.ToString()))
            .Should().BeSameAs(pipe);
    }

    [Fact]
    public async Task GetNorthboundConnection_ColocatedNetworkComponent_UsesLocalPipe()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        // A network component whose host identity is this host: it is co-located, so the controller
        // reaches the databases over the local pipe even though it advertises no remote endpoint.
        var colocated = NetworkComponent(ComponentIdentity.GetLocalHostId(), advertisedEndpoint: null);
        var provider = CreateProvider([colocated], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        result.IsSucc.Should().BeTrue();
        result.Match(Succ: c => c, Fail: e => throw new Exception(e.ToString()))
            .Should().BeSameAs(pipe);
    }

    [Fact]
    public async Task GetNorthboundConnection_RemoteComponentWithoutEndpoint_FailsFast()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        var remote = NetworkComponent("remote-host.example", advertisedEndpoint: null);
        var provider = CreateProvider([remote], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        result.IsFail.Should().BeTrue();
        result.Match(Succ: _ => "", Fail: e => e.Message)
            .Should().Contain("has not advertised");
    }

    [Fact]
    public async Task GetNorthboundConnection_PrefersMostRecentlyHeartbeatingNetworkComponent()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        // A stale registration that still names this host (would be misdetected as co-located) and a
        // fresher remote one. The provider must follow the live remote component, not the stale local.
        var staleLocal = NetworkComponent(
            ComponentIdentity.GetLocalHostId(), advertisedEndpoint: null,
            lastHeartbeat: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10));
        var freshRemote = NetworkComponent(
            "remote-host.example", advertisedEndpoint: null, lastHeartbeat: DateTimeOffset.UtcNow);
        var provider = CreateProvider([staleLocal, freshRemote], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        // It picked the remote component (no endpoint advertised) and failed fast, rather than
        // returning the local pipe it would have for the stale co-located registration.
        result.IsFail.Should().BeTrue();
        result.Match(Succ: _ => "", Fail: e => e.Message)
            .Should().Contain("has not advertised");
    }

    private static OvnNorthboundConnectionProvider CreateProvider(
        IReadOnlyList<ComponentRegistration> active, OvsDbConnection localPipe)
    {
        var ovnSettings = new Mock<IOVNSettings>();
        ovnSettings.SetupGet(s => s.NorthDBConnection).Returns(localPipe);

        return new OvnNorthboundConnectionProvider(
            new Container(),
            new StubRegistry(active),
            ovnSettings.Object,
            Mock.Of<ISystemEnvironment>(),
            NullLogger<OvnNorthboundConnectionProvider>.Instance);
    }

    private static ComponentRegistration NetworkComponent(
        string machineName, string? advertisedEndpoint, DateTimeOffset? lastHeartbeat = null)
    {
        var advertised = new Dictionary<string, string>();
        if (advertisedEndpoint is not null)
            advertised[OvnRemoteEndpoints.NorthboundName] = advertisedEndpoint;

        return new ComponentRegistration
        {
            MachineName = machineName,
            InboundQueue = "q",
            ComponentType = ComponentType.Network,
            AdvertisedEndpoints = advertised,
            LastHeartbeat = lastHeartbeat ?? DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Hand-written stub: <see cref="IComponentRegistryService"/> is internal, which Moq cannot proxy
    /// without marking the production assembly InternalsVisibleTo the proxy generator. Only
    /// GetActiveAsync is exercised here.
    /// </summary>
    private sealed class StubRegistry(IReadOnlyList<ComponentRegistration> active) : IComponentRegistryService
    {
        public Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RecordHeartbeatAsync(Guid componentId, Guid instanceId,
            IReadOnlyDictionary<ConfigDomain, long> appliedConfigVersions, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, long version, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken)
            => Task.FromResult(active);
    }
}
